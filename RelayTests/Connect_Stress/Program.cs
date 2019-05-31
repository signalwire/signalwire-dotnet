using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignalWire.Relay;
using SignalWire.Relay.Calling;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Connect_Stress
{
    internal class Program
    {
        private static ILogger Logger { get; set; }

        private static ManualResetEventSlim sCompleted = new ManualResetEventSlim();
        private static bool sSuccessful = false;

        private static int sCount = 100;

        public static int Main(string[] args)
        {
            // Setup logging to console for Blade and SignalWire
            BladeLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);
            SignalWireLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);

            // Create a logger for this entry point class type
            Logger = SignalWireLogging.CreateLogger<Program>();

            Logger.LogInformation("Started");

            Stopwatch timer = Stopwatch.StartNew();

            // Use environment variables
            string session_host = Environment.GetEnvironmentVariable("SWCLIENT_TEST_SESSION_HOST");
            string session_project = Environment.GetEnvironmentVariable("SWCLIENT_TEST_SESSION_PROJECT");
            string session_token = Environment.GetEnvironmentVariable("SWCLIENT_TEST_SESSION_TOKEN");

            // Make sure we have mandatory options filled in
            if (session_host == null)
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_SESSION_HOST' environment variable");
                return -1;
            }
            if (session_project == null)
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_SESSION_PROJECT' environment variable");
                return -1;
            }
            if (session_token == null)
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_SESSION_TOKEN' environment variable");
                return -1;
            }

            try
            {
                int total = sCount;
                for (int count = 0; count < total; ++count)
                {
                    Logger.LogInformation("Starting {0}", count);

                    // Create the client
                    Client client = new Client(session_project, session_token, host: session_host);
                    // Setup callbacks before the client is started
                    client.OnReady += Client_OnReady;
                    // Start the client
                    client.Connect();
                    Thread.Sleep(100);
                }

                // Wait more than long enough for the test to be completed
                if (!sCompleted.Wait(TimeSpan.FromMinutes(2))) Logger.LogError("Test timed out");
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

            Task.Run(() =>
            {
                client.Dispose();

                if (Interlocked.Decrement(ref sCount) == 0)
                {
                    sSuccessful = true;
                    sCompleted.Set();
                }
            });
        }
    }
}
