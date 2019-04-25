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

namespace Calling_Complex
{
    internal class Program
    {
        /* This complete test requires 2 phone numbers on signalwire, one bound as an inbound DID to relay
         * and the other used for outbound from number validation.
         * This test does the following:
         * - Does a call.receive to receive inbound calls on a configurable receive context
         * - Does a call.begin to the inbound DID as CallA, and does a call.answer on the call to the inbound DID as CallAR which is ignored otherwise
         * - Does a call.connect to the inbound DID as CallB, and does a call.answer on the call to the inbound DID as CallBR which is ignored otherwise
         * - Does a call.end on CallB then CallA
         */
        private static ILogger Logger { get; set; }

        private static ManualResetEventSlim sCompleted = new ManualResetEventSlim();
        private static bool sSuccessful = false;

        private static RelayClient sClient = null;

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

        private static void Client_OnReady(RelayClient client)
        {
            // This is called when the client has established a new session, this is NOT called when a session is restored
            Logger.LogInformation("OnReady");

            // Hook all the callbacks for testing
            client.Calling.OnCallReceived += CallingAPI_OnCallReceived;

            Task.Run(() =>
            {
                // Request that the inbound calls for the given context reach this client
                try { client.Calling.Receive(sCallReceiveContext); }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "CallReceive failed");
                    sCompleted.Set();
                    return;
                }

                // Create the first outbound call leg to the inbound DID associated to the context this client is receiving
                PhoneCall callA = null;
                try
                {
                    callA = client.Calling.NewPhoneCall(Guid.NewGuid().ToString(), sCallToNumber, sCallFromNumber);

                    callA.Begin();
                }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "CallBeginPhone failed");
                    sCompleted.Set();
                    return;
                }

                // Block waiting for a reasonable amount of time for the state to change to answered or ended, this will succeed
                // after answering the call in CallingAPI_OnCallReceived
                if (!callA.WaitForState(TimeSpan.FromSeconds(20), CallState.answered, CallState.ended))
                {
                    Logger.LogError("CallA was not answered or ended in a timely fashion: {0}", callA.State);
                    sCompleted.Set();
                    return;
                }

                // If it's not answered, it ended for some reason
                if (callA.State != CallState.answered)
                {
                    Logger.LogError("CallA was not answered");
                    sCompleted.Set();
                    return;
                }

                // The call was answered, try to connect another outbound call to it
                Call callB = null;
                try
                {
                    // The top level list of the devices represents entries that will be called in serial,
                    // one at a time.  The inner list of devices represents a set of devices to call in
                    // parallel with each other.  Ultimately only one device wins by answering first.
                    callB = callA.Connect(new List<List<CallDevice>>
                    {
                        new List<CallDevice>
                        {
                            new CallDevice
                            {
                                Type = CallDevice.DeviceType.phone,
                                Parameters = new CallDevice.PhoneParams
                                {
                                    ToNumber = sCallToNumber,
                                    FromNumber = sCallFromNumber,
                                }
                            }
                        }
                    });
                }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Connect failed");
                    sCompleted.Set();
                    return;
                }

                // If it was connected then we just hangup both calls
                try
                {
                    callB.Hangup();
                    callA.Hangup();
                }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Hangup failed");
                    sCompleted.Set();
                    return;
                }

                // Mark the test successful and terminate
                sSuccessful = true;
                sCompleted.Set();
            });
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
                }
            });
        }
    }
}
