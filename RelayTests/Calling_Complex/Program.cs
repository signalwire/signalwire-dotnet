﻿using Blade;
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

        private static Client sClient = null;
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

            // Setup the options for the client
            Client.ClientOptions options = new Client.ClientOptions();

            // Use environment variables
            string session_bootstrap = Environment.GetEnvironmentVariable("SWCLIENT_TEST_SESSION_BOOTSTRAP");
            if (!string.IsNullOrWhiteSpace(session_bootstrap)) options.SessionOptions.Bootstrap = new Uri(session_bootstrap);
            string session_project = Environment.GetEnvironmentVariable("SWCLIENT_TEST_SESSION_PROJECT");
            string session_token = Environment.GetEnvironmentVariable("SWCLIENT_TEST_SESSION_TOKEN");
            if (!string.IsNullOrWhiteSpace(session_project) && !string.IsNullOrWhiteSpace(session_token)) options.SessionOptions.Authentication = Client.CreateAuthentication(session_project, session_token);
            sCallReceiveContext = Environment.GetEnvironmentVariable("SWCLIENT_TEST_CALLRECEIVE_CONTEXT");
            sCallToNumber = Environment.GetEnvironmentVariable("SWCLIENT_TEST_CALL_TO_NUMBER");
            sCallFromNumber = Environment.GetEnvironmentVariable("SWCLIENT_TEST_CALL_FROM_NUMBER");

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
            sCallingAPI = new CallingAPI(client);

            // Hook all the callbacks for testing
            sCallingAPI.OnCallCreated += CallingAPI_OnCallCreated;

            sCallingAPI.OnCallReceiveCreated += CallingAPI_OnCallReceiveCreated;

            Task.Run(() =>
            {
                // Request that the inbound calls for the given context reach this client
                try { sCallingAPI.CallReceive(sCallReceiveContext); }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "CallReceive failed");
                    sCompleted.Set();
                    return;
                }

                // Create the first outbound call leg to the inbound DID associated to the context this client is receiving
                Call callA = null;
                try
                {
                    callA = sCallingAPI.CreateCall(Guid.NewGuid().ToString());
                    callA.OnConnectConnected += OnCallConnectConnected;
                    callA.OnConnectFailed += OnCallConnectFailed;

                    callA.OnStateChange += OnCallStateChange;
                    callA.OnAnswered += OnCallAnswered;
                    callA.OnEnded += OnCallEnded;

                    callA.BeginPhone(sCallToNumber, sCallFromNumber);
                }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "CallBeginPhone failed");
                    sCompleted.Set();
                    return;
                }

                // Block waiting for a reasonable amount of time for the state to change to answered or ended, this will succeed
                // after answering the call in CallingAPI_OnCallReceiveCreated
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

        private static void CallingAPI_OnCallCreated(CallingAPI api, Call call)
        {
            Logger.LogInformation("OnCallCreated: {0}, {1}", call.CallID, call.State);
        }

        private static void CallingAPI_OnCallReceiveCreated(CallingAPI api, Call call, CallEventParams.ReceiveParams receiveParams)
        {
            Logger.LogInformation("OnCallReceiveCreated: {0}, {1}", call.CallID, call.State);

            Task.Run(() =>
            {
                try { call.Answer(); }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "CallAnswer failed");
                    sCompleted.Set();
                }
            });
        }

        private static void OnCallConnectConnected(CallingAPI api, Call call, Call callConnected, CallEventParams.ConnectParams connectParams)
        {
            Logger.LogInformation("OnCallConnectConnected: {0}, {1} to {2}, {3}", call.CallID, call.State, callConnected.CallID, callConnected.State);
        }

        private static void OnCallConnectFailed(CallingAPI api, Call call, CallEventParams.ConnectParams connectParams)
        {
            Logger.LogInformation("OnCallConnectFailed: {0}, {1}", call.CallID, call.State);
        }

        private static void OnCallStateChange(CallingAPI api, Call call, CallState oldState, CallEventParams.StateParams stateParams)
        {
            Logger.LogInformation("OnCallStateChange: {0}, {1} to {2}", call.CallID, oldState, stateParams.CallState);
        }

        private static void OnCallAnswered(CallingAPI api, Call call, CallEventParams.StateParams stateParams)
        {
            Logger.LogInformation("OnCallAnswered: {0}, {1}", call.CallID, call.State);
        }

        private static void OnCallEnded(CallingAPI api, Call call, CallEventParams.StateParams stateParams)
        {
            Logger.LogInformation("OnCallEnded: {0}, {1}", call.CallID, call.State);
        }
    }
}