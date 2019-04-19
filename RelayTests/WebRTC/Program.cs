using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SignalWire;
using SignalWire.WebRTC;
using System;
using System.Diagnostics;
using System.Threading;

namespace WebRTC
{
    internal class Program
    {
        private static ILogger Logger { get; set; }

        private static ManualResetEventSlim sCompleted = new ManualResetEventSlim();
        private static bool sSuccessful = false;

        private static Client sClient = null;
        private static WebRTCAPI sWebRTCAPI = null;

        public static int Main(string[] args)
        {
            // Setup logging to console for Blade and SignalWire
            BladeLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);
            SignalWireLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);

            // Create a logger for this entry point class type
            Logger = SignalWireLogging.CreateLogger<Program>();

            Logger.LogInformation("Started");

            Stopwatch timer = Stopwatch.StartNew();

            // Setup the options for the client
            Client.ClientOptions options = new Client.ClientOptions();

            // Use environment variables
            string session_bootstrap = Environment.GetEnvironmentVariable("SWCLIENT_TEST_SESSION_BOOTSTRAP");
            if (!string.IsNullOrWhiteSpace(session_bootstrap)) options.SessionOptions.Bootstrap = new Uri(session_bootstrap);
            string session_project = Environment.GetEnvironmentVariable("SWCLIENT_TEST_SESSION_PROJECT");
            string session_token = Environment.GetEnvironmentVariable("SWCLIENT_TEST_SESSION_TOKEN");
            if (!string.IsNullOrWhiteSpace(session_project) && !string.IsNullOrWhiteSpace(session_token)) options.SessionOptions.Authentication = Client.CreateAuthentication(session_project, session_token);

            // Make sure we have mandatory options filled in
            if (options.SessionOptions.Bootstrap == null)
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_SESSION_BOOTSTRAP' environment variable");
                return -1;
            }
            if (options.SessionOptions.Authentication == null)
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_SESSION_PROJECT' and/or 'SWCLIENT_TEST_SESSION_TOKEN' environment variables");
                return -1;
            }

            try
            {
                // Create the client
                using (sClient = new Client(options))
                {
                    // Setup callbacks before the client is started
                    sClient.OnReady += Client_OnReady;

                    // Start the client
                    sClient.Start();

                    // Wait more than long enough for the test to be completed
                    if (!sCompleted.Wait(TimeSpan.FromMinutes(2))) Logger.LogError("Test timed out");
                }
            }
            catch (Exception exc)
            {
                Logger.LogError(exc, "Client startup failed");
            }

            timer.Stop();

            // Report test outcome
            if (!sSuccessful) Logger.LogError("Completed unsuccessfully: {0} elapsed", timer.Elapsed);
            else Logger.LogInformation("Completed successfully: {0} elapsed", timer.Elapsed);

#if DEBUG
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
#endif
            return sSuccessful ? 0 : -1;
        }

        private static void Client_OnReady(Client client)
        {
            // This is called when the client has established a new session, this is NOT called when a session is restored
            Logger.LogInformation("OnReady");

            // Create the api associating it to the client for transport
            sWebRTCAPI = new WebRTCAPI(client);

            // Hook all the callbacks for testing
            sWebRTCAPI.OnRTCEvent += WebRTCAPI_OnRTCEvent;

            sWebRTCAPI.Setup().Wait();

            sWebRTCAPI.Configure("shane", "shane.webrtc.swire.io");

            sWebRTCAPI.Message(JObject.Parse("{\"jsonrpc\":\"2.0\",\"id\":\"e24b1643-47a6-45d0-aed4-4811974ec794\",\"method\":\"login\",\"params\":{\"login\":\"1008@test.signalwire.com\",\"passwd\":\"...\",\"loginParams\":{},\"userVariables\":{}}}"));

            // Mark the test successful and terminate
            sSuccessful = true;
            sCompleted.Set();
        }

        private static void WebRTCAPI_OnRTCEvent(WebRTCAPI api, BroadcastParams broadcastParams, WebRTCEventParams webRTCEventParams)
        {
            Logger.LogInformation("OnRTCEvent:");
        }
    }
}
