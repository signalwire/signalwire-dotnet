using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignalWire;
using SignalWire.Calling;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Calling_Record
{
    internal class Program
    {
        private static ILogger Logger { get; set; }

        private static ManualResetEventSlim sCompleted = new ManualResetEventSlim();
        private static bool sSuccessful = false;

        private static RelayClient sClient = null;
        private static CallingAPI sCallingAPI = null;

        private static string sCallReceiveContext = null;
        private static string sCallToNumber = null;
        private static string sCallFromNumber = null;

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
            sCallReceiveContext = Environment.GetEnvironmentVariable("SWCLIENT_TEST_CALLRECEIVE_CONTEXT");
            sCallToNumber = Environment.GetEnvironmentVariable("SWCLIENT_TEST_CALL_TO_NUMBER");
            sCallFromNumber = Environment.GetEnvironmentVariable("SWCLIENT_TEST_CALL_FROM_NUMBER");

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
            if (sCallReceiveContext == null)
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_CALLRECEIVE_CONTEXT' environment variable");
                return -1;
            }
            if (sCallToNumber == null)
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_CALL_TO_NUMBER' environment variable");
                return -1;
            }
            if (sCallFromNumber == null)
            {
                Logger.LogError("Missing 'SWCLIENT_TEST_CALL_FROM_NUMBER' environment variable");
                return -1;
            }

            try
            {
                // Create the client
                using (sClient = new RelayClient(session_host, session_project, session_token))
                {
                    // Setup callbacks before the client is started
                    sClient.OnReady += Client_OnReady;

                    // Start the client
                    sClient.Connect();

                    // Wait more than long enough for the test to be completed
                    if (!sCompleted.Wait(TimeSpan.FromMinutes(5))) Logger.LogError("Test timed out");
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

        private static void Client_OnReady(RelayClient client)
        {
            // This is called when the client has established a new session, this is NOT called when a session is restored
            Logger.LogInformation("OnReady");

            // Create the api associating it to the client for transport
            sCallingAPI = new CallingAPI(client);

            // Hook all the callbacks for testing
            sCallingAPI.OnCallReceived += CallingAPI_OnCallReceived;

            Task.Run(() =>
            {
                // Request that the inbound calls for the given context reach this client
                try { sCallingAPI.Receive(sCallReceiveContext); }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "CallReceive failed");
                    sCompleted.Set();
                    return;
                }
            });
        }

        private static void CallingAPI_OnCallCreated(CallingAPI api, Call call)
        {
            Logger.LogInformation("OnCallCreated: {0}, {1}", call.CallID, call.State);
        }

        private static void CallingAPI_OnCallReceived(CallingAPI api, Call call, CallEventParams.ReceiveParams receiveParams)
        {
            Logger.LogInformation("OnCallReceived: {0}, {1}", call.CallID, call.State);

            Task.Run(() =>
            {
                try { call.Answer(); }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Answer failed");
                    sCompleted.Set();
                    return;
                }

                try
                {
                    call.OnRecordStateChange += OnCallRecordStateChange;

                    call.StartRecord("bobs_your_uncle", CallRecordType.audio, new CallRecordAudio()
                    {
                        // Default parameters
                    });
                }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "StartRecord failed");
                    sCompleted.Set();
                    return;
                }
            });
        }

        private static void OnCallRecordStateChange(CallingAPI api, Call call, CallEventParams.RecordParams recordParams)
        {
            Logger.LogInformation("OnCallRecordStateChange: {0}, {1} for {2}, {3}", call.CallID, call.State, recordParams.ControlID, recordParams.State);
            if (recordParams.State == CallEventParams.RecordParams.RecordState.finished || recordParams.State == CallEventParams.RecordParams.RecordState.no_input)
            {
                Logger.LogInformation("OnCallRecordStateChange: {0}, {1}, {2}", recordParams.Duration, recordParams.Size, recordParams.URL);
                sSuccessful = true;
                sCompleted.Set();
            }
        }
    }
}
