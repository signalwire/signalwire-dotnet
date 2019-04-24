using Blade;
using Microsoft.Extensions.Logging;
using SignalWire;
using SignalWire.Calling;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CallingCall_SimpleForwarding
{
    internal class Program
    {
        private static ILogger Logger { get; set; }

        private static RelayClient sClient = null;
        private static CallingAPI sCallingAPI = null;

        private static string sCallReceiveContext = null;
        private static string sCallToNumber = null;
        private static string sCallFromNumber = null;

        public static void Main(string[] args)
        {
            // Setup logging to console for Blade and SignalWire
            BladeLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);
            SignalWireLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);

            // Create a logger for this entry point class type
            Logger = SignalWireLogging.CreateLogger<Program>();

            Logger.LogInformation("Started");

            // Use environment variables
            string session_bootstrap = Environment.GetEnvironmentVariable("SWCLIENT_EXAMPLE_SESSION_BOOTSTRAP");
            string session_project = Environment.GetEnvironmentVariable("SWCLIENT_EXAMPLE_SESSION_PROJECT");
            string session_token = Environment.GetEnvironmentVariable("SWCLIENT_EXAMPLE_SESSION_TOKEN");
            sCallReceiveContext = Environment.GetEnvironmentVariable("SWCLIENT_EXAMPLE_CALLRECEIVE_CONTEXT");
            sCallToNumber = Environment.GetEnvironmentVariable("SWCLIENT_EXAMPLE_CALL_TO_NUMBER");
            sCallFromNumber = Environment.GetEnvironmentVariable("SWCLIENT_EXAMPLE_CALL_FROM_NUMBER");

            // Make sure we have mandatory options filled in
            if (session_bootstrap == null)
            {
                Logger.LogError("Missing 'SWCLIENT_EXAMPLE_SESSION_BOOTSTRAP' environment variable");
                return;
            }
            if (session_project == null)
            {
                Logger.LogError("Missing 'SWCLIENT_EXAMPLE_SESSION_PROJECT' environment variable");
                return;
            }
            if (session_token == null)
            {
                Logger.LogError("Missing 'SWCLIENT_EXAMPLE_SESSION_TOKEN' environment variable");
                return;
            }
            if (sCallReceiveContext == null)
            {
                Logger.LogError("Missing 'SWCLIENT_EXAMPLE_CALLRECEIVE_CONTEXT' environment variable");
                return;
            }
            if (sCallToNumber == null)
            {
                Logger.LogError("Missing 'SWCLIENT_EXAMPLE_CALL_TO_NUMBER' environment variable");
                return;
            }
            if (sCallFromNumber == null)
            {
                Logger.LogError("Missing 'SWCLIENT_EXAMPLE_CALL_FROM_NUMBER' environment variable");
                return;
            }

            try
            {
                // Create the client
                using (sClient = new RelayClient(session_bootstrap, session_project, session_token))
                {
                    // Setup callbacks before the client is started
                    sClient.OnReady += Client_OnReady;

                    // Start the client
                    sClient.Start();

                    Console.ReadKey(true);
                }
            }
            catch (Exception exc)
            {
                Logger.LogError(exc, "Client startup failed");
            }
        }

        private static void Client_OnReady(RelayClient client)
        {
            // This is called when the client has established a new session, this is NOT called when a session is restored
            Logger.LogInformation("OnReady");

            // Create the api associating it to the client for transport
            sCallingAPI = new CallingAPI(client);

            // Hook the callback that occurs when an inbound call is created
            sCallingAPI.OnCallReceived += CallingAPI_OnCallReceived;

            // Request that the inbound calls for the given context reach this client
            try { sCallingAPI.Receive(sCallReceiveContext); }
            catch (Exception exc)
            {
                Logger.LogError(exc, "CallReceive failed");
                return;
            }
        }

        private static void CallingAPI_OnCallReceived(CallingAPI api, Call call, CallEventParams.ReceiveParams receiveParams)
        {
            // This is called when the client is informed that a new inbound call has been created
            Logger.LogInformation("OnCallReceived: {0}, {1}", call.CallID, call.State);

            // Answer the inbound call
            try { call.Answer(); }
            catch (Exception exc)
            {
                Logger.LogError(exc, "CallAnswer failed");
                return;
            }

            Call callB = null;
            try
            {
                // The top level list of the devices represents entries that will be called in serial,
                // one at a time.  The inner list of devices represents a set of devices to call in
                // parallel with each other.  Ultimately only one device wins by answering first.
                callB = call.Connect(new List<List<CallDevice>>
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
                return;
            }

            // Wait upto 15 seconds for the connected side to end then hangup
            callB.WaitForState(TimeSpan.FromSeconds(15), CallState.ended);
            call.Hangup();
        }
    }
}
