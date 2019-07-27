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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Calling_Detect
{
    internal class Program
    {
        private static ILogger Logger { get; set; }

        private static ManualResetEventSlim sHumanCompleted = new ManualResetEventSlim();
        private static bool sRunHuman = true;
        private static bool sHumanSuccessful = !sRunHuman;

        private static ManualResetEventSlim sMachineCompleted = new ManualResetEventSlim();
        private static bool sRunMachine = true;
        private static bool sMachineSuccessful = !sRunMachine;

        private static ManualResetEventSlim sFaxCompleted = new ManualResetEventSlim();
        private static bool sRunFax = false;
        private static bool sFaxSuccessful = !sRunFax;

        private static ManualResetEventSlim sDigitCompleted = new ManualResetEventSlim();
        private static bool sRunDigit = true;
        private static bool sDigitSuccessful = !sRunDigit;

        private static Client sClient = null;
        private static CallingAPI sCallingAPI = null;
        private static DetectAction sDetect = null;

        private static string sCallReceiveContext = null;
        private static string sCallToHumanNumber = null;
        private static string sCallToMachineNumber = null;
        private static string sCallToFaxNumber = null;
        private static string sCallToDigitNumber = null;
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
            string session_host = Environment.GetEnvironmentVariable("TEST_SESSION_HOST");
            string session_project = Environment.GetEnvironmentVariable("TEST_SESSION_PROJECT");
            string session_token = Environment.GetEnvironmentVariable("TEST_SESSION_TOKEN");
            sCallReceiveContext = Environment.GetEnvironmentVariable("TEST_CONTEXT");
            sCallToHumanNumber = Environment.GetEnvironmentVariable("TEST_TO_HUMAN_NUMBER");
            sCallToMachineNumber = Environment.GetEnvironmentVariable("TEST_TO_MACHINE_NUMBER");
            sCallToFaxNumber = Environment.GetEnvironmentVariable("TEST_TO_FAX_NUMBER");
            sCallToDigitNumber = Environment.GetEnvironmentVariable("TEST_TO_DIGIT_NUMBER");
            sCallFromNumber = Environment.GetEnvironmentVariable("TEST_FROM_NUMBER");

            // Make sure we have mandatory options filled in
            if (session_host == null)
            {
                Logger.LogError("Missing 'TEST_SESSION_HOST' environment variable");
                return -1;
            }
            if (session_project == null)
            {
                Logger.LogError("Missing 'TEST_SESSION_PROJECT' environment variable");
                return -1;
            }
            if (session_token == null)
            {
                Logger.LogError("Missing 'TEST_SESSION_TOKEN' environment variable");
                return -1;
            }
            if (sCallReceiveContext == null)
            {
                Logger.LogError("Missing 'TEST_CONTEXT' environment variable");
                return -1;
            }
            if (sCallToHumanNumber == null)
            {
                Logger.LogError("Missing 'TEST_TO_HUMAN_NUMBER' environment variable");
                return -1;
            }
            if (sCallToMachineNumber == null)
            {
                Logger.LogError("Missing 'TEST_TO_MACHINE_NUMBER' environment variable");
                return -1;
            }
            if (sCallToFaxNumber == null)
            {
                Logger.LogError("Missing 'TEST_TO_FAX_NUMBER' environment variable");
                return -1;
            }
            if (sCallToDigitNumber == null)
            {
                Logger.LogError("Missing 'TEST_TO_DIGIT_NUMBER' environment variable");
                return -1;
            }
            if (sCallFromNumber == null)
            {
                Logger.LogError("Missing 'TEST_FROM_NUMBER' environment variable");
                return -1;
            }

            try
            {
                // Create the client
                using (sClient = new Client(session_project, session_token, host: session_host))
                {
                    // Setup callbacks before the client is started
                    sClient.OnReady += Client_OnReady;

                    // Start the client
                    sClient.Connect();

                    // Wait more than long enough for the tests to be completed
                    var handles = new[] {
                        sHumanCompleted.WaitHandle,
                        sMachineCompleted.WaitHandle,
                        sFaxCompleted.WaitHandle,
                        sDigitCompleted.WaitHandle,
                    };
                    if (!WaitHandle.WaitAll(handles, TimeSpan.FromMinutes(2))) Logger.LogError("At least one test timed out");
                }
            }
            catch (Exception exc)
            {
                Logger.LogError(exc, "Client startup failed");
            }

            timer.Stop();

            Logger.LogInformation("[Report] Tests completed, {1} elapsed", timer.Elapsed);

            // Report test outcomes
            Action<string, bool> report = (id, success) =>
            {
                if (!success) Logger.LogError("[Report] {0} completed unsuccessfully", id);
                else Logger.LogInformation("[Report] {0} completed successfully", id);
            };
            if (sRunHuman) report("Human", sHumanSuccessful);
            if (sRunMachine) report("Machine", sMachineSuccessful);
            if (sRunFax) report("Fax", sFaxSuccessful);
            if (sRunDigit) report("Digit", sDigitSuccessful);

#if DEBUG
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
#endif
            return
                new[] {
                    sHumanSuccessful,
                    sMachineSuccessful,
                    sFaxSuccessful,
                    sDigitSuccessful,
                }.All(b => b) ? 0 : -1;
        }

        private static void Client_OnReady(Client client)
        {
            // This is called when the client has established a new session, this is NOT called when a session is restored
            Logger.LogInformation("OnReady");

            // Create the api associating it to the client for transport
            sCallingAPI = client.Calling;

            Task.Run(() =>
            {
                CallSetup(
                    "Human",
                    sCallToHumanNumber,
                    sRunHuman,
                    sHumanCompleted,
                    OnCallDetectHuman,
                    () => sHumanSuccessful = true,
                    new CallDetect()
                    {
                        Type = CallDetect.DetectType.machine,
                        Parameters = new CallDetect.MachineParams()
                        {

                        },
                    });

                CallSetup(
                    "Machine",
                    sCallToMachineNumber,
                    sRunMachine,
                    sMachineCompleted,
                    OnCallDetectMachine,
                    () => sMachineSuccessful = true,
                    new CallDetect()
                    {
                        Type = CallDetect.DetectType.machine,
                        Parameters = new CallDetect.MachineParams()
                        {

                        },
                    });

                CallSetup(
                    "Fax",
                    sCallToFaxNumber,
                    sRunFax,
                    sFaxCompleted,
                    OnCallDetectFax,
                    () => sFaxSuccessful = true,
                    new CallDetect()
                    {
                        Type = CallDetect.DetectType.fax,
                        Parameters = new CallDetect.FaxParams()
                        {

                        },
                    });

                CallSetup(
                    "Digit",
                    sCallToDigitNumber,
                    sRunDigit,
                    sDigitCompleted,
                    OnCallDetectDigit,
                    () => sDigitSuccessful = true,
                    new CallDetect()
                    {
                        Type = CallDetect.DetectType.digit,
                        Parameters = new CallDetect.DigitParams()
                        {

                        },
                    });
            });
        }

        private static void CallSetup(
            string tag,
            string toNumber,
            bool shouldRun,
            ManualResetEventSlim waitHandle,
            Func<CallingEventParams.DetectParams, bool> isDetectValid,
            Action setSuccessfulDetection,
            CallDetect detect)
        {
            if (!shouldRun)
            {
                waitHandle.Set();
                sDetect = null;
                return;
            }
 
            Logger.LogInformation("[{0}] Beginning setup for call to {1}", tag, toNumber);
            PhoneCall call = sCallingAPI.NewPhoneCall(toNumber, sCallFromNumber);
            Logger.LogInformation("[{0}] Call created, associating events", tag);
            call.OnDetect += (CallingAPI api, Call detectedCall, CallingEventParams detectEventParams, CallingEventParams.DetectParams detectParams) =>
            {
                if (detectParams.Detect.Parameters.Event == "READY") return;
                if (detectParams.Detect.Parameters.Event == "finished")
                {
                    if (isDetectValid(detectParams))
                    {
                        Logger.LogInformation("[{0}] Completed successfully", tag);
                        setSuccessfulDetection();
                    }
                    else
                    {
                        Logger.LogError("[{0}] Unsuccessful", tag);
                    }
                    sDetect = null;
                    return;
                }

                Logger.LogInformation("[{0}] OnDetect with ID: {1}, {2} for {3}", tag, detectedCall.ID, detectParams.Detect.Type, detectParams.ControlID);
                if (isDetectValid(detectParams))
                {
                    setSuccessfulDetection();
                    Task.Run(() =>
                    {
                        sDetect.Stop();
                        sDetect = null;
                    });
                    Logger.LogInformation("[{0}] Completed successfully", tag);
                }
                else
                {
                    Logger.LogError("[{0}] Unsuccessful", tag);
                }
            };
            Logger.LogInformation("[{0}] OnDetect associated", tag);
            call.OnEnded += (CallingAPI api, Call endedCall, CallingEventParams stateEventParams, CallingEventParams.StateParams stateParams) =>
            {
                Logger.LogInformation("[{0}] OnEnded with ID: {1}", tag, endedCall.ID);
                sDetect = null;
                waitHandle.Set();
                Logger.LogInformation("[{0}] OnEnded complete", tag);
            };
            Logger.LogInformation("[{0}] OnEnded associated", tag);
            call.OnAnswered += (CallingAPI api, Call answeredCall, CallingEventParams answerEventParams, CallingEventParams.StateParams stateParams) =>
            {
                Logger.LogInformation("[{0}] OnAnswered with ID: {1}", tag, answeredCall.ID);

                Task.Run(() =>
                {
                    try
                    {
                        Logger.LogInformation("[{0}] Performing detect", tag);
                        sDetect = answeredCall.DetectAsync(detect);
                        Logger.LogInformation("[{0}] Detect performed", tag);
                    }
                    catch (Exception exc)
                    {
                        Logger.LogError(exc, $"[{tag}] call.Detect failed");
                        waitHandle.Set();
                        sDetect = null;
                        return;
                    }
                });
            };
            Logger.LogInformation("[{0}] OnAnswered associated", tag);
            call.OnConnectStateChange += (CallingAPI api, Call connectStateChangeCall, CallingEventParams connectStateChangeEventParams, CallingEventParams.ConnectParams connectStateChangeParams) =>
            {
                Logger.LogInformation("[{0}] OnConnectStateChange: {1}", tag, connectStateChangeParams.State);
            };
            Logger.LogInformation("[{0}] OnConnectStateChange associated", tag);
            call.OnReceiveStateChange += (CallingAPI api, Call receiveStateChangeCall, CallingEventParams receiveStateChangeEventParams, CallingEventParams.ReceiveParams receiveStateChangeParams) =>
            {
                Logger.LogInformation("[{0}] OnReceiveStateChange: {1}", tag, receiveStateChangeParams.CallState);
            };
            Logger.LogInformation("[{0}] OnReceiveStateChange associated", tag);
            call.OnStateChange += (CallingAPI api, Call stateChangeCall, CallingEventParams stateChangeEventParams, CallingEventParams.StateParams stateChangeParams) =>
            {
                Logger.LogInformation("[{0}] OnStateChange: {1}", tag, stateChangeParams.CallState);
            };
            Logger.LogInformation("[{0}] OnStateChange associated", tag);

            try
            {
                Logger.LogInformation("[{0}] Executing call", tag);
                var dialAction = call.Dial();
                Logger.LogInformation("[{0}] Call executed", tag);
            }
            catch (Exception exc)
            {
                Logger.LogError(exc, $"[{tag}] call.DialAsync failed");
                waitHandle.Set();
                sDetect = null;
                return;
            }
        }

        private static bool OnCallDetectHuman(CallingEventParams.DetectParams detectParams)
        {
            return
                detectParams.Detect.Type == CallingEventParams.DetectParams.DetectType.machine &&
                detectParams.Detect.Parameters.Event == "HUMAN";
        }

        private static bool OnCallDetectMachine(CallingEventParams.DetectParams detectParams)
        {
            return
                detectParams.Detect.Type == CallingEventParams.DetectParams.DetectType.machine &&
                detectParams.Detect.Parameters.Event == "MACHINE";
        }

        private static bool OnCallDetectFax(CallingEventParams.DetectParams detectParams)
        {
            return detectParams.Detect.Type == CallingEventParams.DetectParams.DetectType.fax;
        }

        private static bool OnCallDetectDigit(CallingEventParams.DetectParams detectParams)
        {
            // TODO
            // Improve test by looking for specific digit sequence or whatever
            return detectParams.Detect.Type == CallingEventParams.DetectParams.DetectType.digit;
        }
    }
}