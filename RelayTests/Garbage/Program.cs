using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SignalWire.Relay;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Garbage
{
    internal class Program
    {
        private static ILogger Logger { get; set; }

        private static ManualResetEventSlim sCompleted = new ManualResetEventSlim();
        private static bool sSuccessful = false;

        private static Client sClient = null;

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
                using (sClient = new Client(session_project, session_token, host: session_host))
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

        private static void Client_OnReady(Client client)
        {
            // This is called when the client has established a new session, this is NOT called when a session is restored
            Logger.LogInformation("OnReady");

            Task.Run(() =>
            {
                if (!Test1(client)) return;
                if (!Test2(client)) return;
                if (!Test3(client)) return;
                if (!Test4(client)) return;

                // Mark the test successful and terminate
                sSuccessful = true;
                sCompleted.Set();
            });
        }

        private static bool Test1(Client client)
        {
            // Test execute of signalwire setup with no inner params object
            Task<ResponseTaskResult<ExecuteResult>> setupTask = null;
            ResponseTaskResult<ExecuteResult> setupTaskResult = null;

            try
            {
                setupTask = client.Session.ExecuteAsync("signalwire", "setup", null);
                setupTaskResult = setupTask.Result;
            }
            catch (AggregateException exc)
            {
                if (exc.InnerExceptions.Count != 1)
                {
                    Logger.LogError(exc, "Received an unexpected count of errors on signalwire setup");
                    sCompleted.Set();
                    return false;
                }
                var inner = exc.InnerExceptions[0];
                if (inner.GetType() != typeof(InvalidOperationException))
                {
                    // Successful, it detected bad arguments and responded
                    Logger.LogError(exc, "Received an exception that was not InvalidOperationException on signalwire setup");
                    sCompleted.Set();
                    return false;
                }
            }
            catch (TimeoutException exc)
            {
                // Timeout means there was no response, that's bad
                Logger.LogError(exc, "Received a timeout on signalwire setup");
                sCompleted.Set();
                return false;
            }
            catch (Exception exc)
            {
                // Anything else failed in an unexpected way
                Logger.LogError(exc, "Received a unexpected error on signalwire setup");
                sCompleted.Set();
                return false;
            }

            if (setupTaskResult != null)
            {
                Logger.LogError("Result is not null as expected");
                sCompleted.Set();
                return false;
            }

            return true;
        }

        private static bool Test2(Client client)
        {
            // Test execute of signalwire setup with an empty inner params object (no service field)
            Task<ResponseTaskResult<ExecuteResult>> setupTask = null;
            ResponseTaskResult<ExecuteResult> setupTaskResult = null;

            try
            {
                setupTask = client.Session.ExecuteAsync("signalwire", "setup", new JObject());
                setupTaskResult = setupTask.Result;
            }
            catch (AggregateException exc)
            {
                if (exc.InnerExceptions.Count != 1)
                {
                    Logger.LogError(exc, "Received an unexpected count of errors on signalwire setup");
                    sCompleted.Set();
                    return false;
                }
                var inner = exc.InnerExceptions[0];
                if (inner.GetType() != typeof(ArgumentException))
                {
                    // Successful, it detected bad arguments and responded
                    Logger.LogError(exc, "Received an exception that was not ArgumentException on signalwire setup");
                    sCompleted.Set();
                    return false;
                }
            }
            catch (TimeoutException exc)
            {
                // Timeout means there was no response, that's bad
                Logger.LogError(exc, "Received a timeout on signalwire setup");
                sCompleted.Set();
                return false;
            }
            catch (Exception exc)
            {
                // Anything else failed in an unexpected way
                Logger.LogError(exc, "Received a unexpected error on signalwire setup");
                sCompleted.Set();
                return false;
            }

            if (setupTaskResult != null)
            {
                Logger.LogError("Result is not null as expected");
                sCompleted.Set();
                return false;
            }

            return true;
        }

        private static bool Test3(Client client)
        {
            // Test execute of signalwire setup with an inner params object containing a service field that has an invalid value
            Task<ResponseTaskResult<ExecuteResult>> setupTask = null;
            ResponseTaskResult<ExecuteResult> setupTaskResult = null;

            try
            {
                setupTask = client.Session.ExecuteAsync("signalwire", "setup", new JObject { ["service"] = "invalid" });
                setupTaskResult = setupTask.Result;
            }
            catch (AggregateException exc)
            {
                if (exc.InnerExceptions.Count != 1)
                {
                    Logger.LogError(exc, "Received an unexpected count of errors on signalwire setup");
                    sCompleted.Set();
                    return false;
                }
                var inner = exc.InnerExceptions[0];
                if (inner.GetType() != typeof(ArgumentException))
                {
                    // Successful, it detected bad arguments and responded
                    Logger.LogError(exc, "Received an exception that was not ArgumentException on signalwire setup");
                    sCompleted.Set();
                    return false;
                }
            }
            catch (TimeoutException exc)
            {
                // Timeout means there was no response, that's bad
                Logger.LogError(exc, "Received a timeout on signalwire setup");
                sCompleted.Set();
                return false;
            }
            catch (Exception exc)
            {
                // Anything else failed in an unexpected way
                Logger.LogError(exc, "Received a unexpected error on signalwire setup");
                sCompleted.Set();
                return false;
            }

            if (setupTaskResult != null)
            {
                Logger.LogError("Result is not null as expected");
                sCompleted.Set();
                return false;
            }

            return true;
        }

        private static bool Test4(Client client)
        {
            // Test execute of signalwire setup with an inner params object containing a service field that has a valid value but an invalid protocol for restoring
            Task<ResponseTaskResult<ExecuteResult>> setupTask = null;
            ResponseTaskResult<ExecuteResult> setupTaskResult = null;

            try
            {
                setupTask = client.Session.ExecuteAsync("signalwire", "setup", new JObject { ["service"] = "calling", ["protocol"] = "invalid" });
                setupTaskResult = setupTask.Result;
            }
            catch (AggregateException exc)
            {
                if (exc.InnerExceptions.Count != 1)
                {
                    Logger.LogError(exc, "Received an unexpected count of errors on signalwire setup");
                    sCompleted.Set();
                    return false;
                }
                var inner = exc.InnerExceptions[0];
                if (inner.GetType() != typeof(InvalidOperationException))
                {
                    // Successful, it detected bad arguments and responded
                    Logger.LogError(exc, "Received an exception that was not InvalidOperationException on signalwire setup");
                    sCompleted.Set();
                    return false;
                }
            }
            catch (TimeoutException exc)
            {
                // Timeout means there was no response, that's bad
                Logger.LogError(exc, "Received a timeout on signalwire setup");
                sCompleted.Set();
                return false;
            }
            catch (Exception exc)
            {
                // Anything else failed in an unexpected way
                Logger.LogError(exc, "Received a unexpected error on signalwire setup");
                sCompleted.Set();
                return false;
            }

            if (setupTaskResult != null)
            {
                Logger.LogError("Result is not null as expected");
                sCompleted.Set();
                return false;
            }

            return true;
        }
    }
}
