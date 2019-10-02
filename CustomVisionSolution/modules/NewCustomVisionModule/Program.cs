namespace NewCustomVisionModule
{
  using System;
  using System.IO;
  using System.Runtime.Loader;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.Azure.Devices.Client;
  using Microsoft.Azure.Devices.Client.Transport.Mqtt;
  using System.Net.Http;
  using System.Net.Http.Headers;

  class Program
  {
    static int counter;
    private static ModuleClient ioTHubModuleClient;

    static void Main(string[] args)
    {
      Init().Wait();

      // Wait until the app unloads or is cancelled
      var cts = new CancellationTokenSource();
      AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
      Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
      WhenCancelled(cts.Token).Wait();
    }

    /// <summary>
    /// Handles cleanup operations when app is cancelled or unloads
    /// </summary>
    public static Task WhenCancelled(CancellationToken cancellationToken)
    {
      var tcs = new TaskCompletionSource<bool>();
      cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
      return tcs.Task;
    }

    /// <summary>
    /// Initializes the ModuleClient and sets up the callback to receive
    /// messages containing temperature information
    /// </summary>
    static async Task Init()
    {
      MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
      ITransportSettings[] settings = { mqttSetting };

      // Open a connection to the Edge runtime
      ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
      await ioTHubModuleClient.OpenAsync();
      Console.WriteLine("IoT Hub module client initialized.");

      // Get the image path and adress of the classifier module (set in deployment.template.json)
      var image_path = Environment.GetEnvironmentVariable("IMAGE_PATH");
      var image_processing_endpoint = Environment.GetEnvironmentVariable("IMAGE_PROCESSING_ENDPOINT");

      // Create a new HttpClient to make a request to the classifier
      var client = new HttpClient();
      HttpResponseMessage response;

      var filebytes = File.ReadAllBytes(image_path);
      using (var content = new ByteArrayContent(filebytes))
      {
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        response = await client.PostAsync(image_processing_endpoint, content);
        Console.WriteLine(response.Content.ReadAsStringAsync().Result);

        var message = new Message(Encoding.ASCII.GetBytes(response.Content.ReadAsStringAsync().Result));

        // Send the message to the IoT Hub
        await ioTHubModuleClient.SendEventAsync("output1", message);
      }
    }
  }
}
