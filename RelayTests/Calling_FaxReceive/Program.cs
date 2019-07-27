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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Calling_ReceiveFax
{
    internal class TestConsumer : Consumer
    {
        private string Document { get; set; }
        private string ToNumber { get; set; }
        private string FromNumber { get; set; }

        internal TestConsumer(string host, string project, string token, string context, string document, string toNumber, string fromNumber)
        {
            Host = host;
            Project = project;
            Token = token;
            Contexts = new List<string> { context };
            Document = document;
            ToNumber = toNumber;
            FromNumber = fromNumber;
        }

        private ILogger Logger { get; } = SignalWireLogging.CreateLogger<TestConsumer>();

        internal ManualResetEventSlim Completed { get; } = new ManualResetEventSlim();

        internal bool Successful { get; private set; } = false;

        // This is executed in a new thread each time, so it is safe to use blocking calls
        protected override void Ready()
        {
            DialResult resultDial = Client.Calling.DialPhone(ToNumber, FromNumber);

            if (!resultDial.Successful)
            {
                Logger.LogError("Call was not answered");
                return;
            }

            Call call = resultDial.Call;

            FaxResult sendResult = call.FaxSend(Document);

            if (!sendResult.Successful)
            {
                Successful = false;
                Logger.LogError("Send fax was unsuccessful");
            }
        }

        protected override void OnIncomingCall(Call call)
        {
            AnswerResult resultAnswer = call.Answer();

            if (!resultAnswer.Successful)
            {
                // The call was not answered successfully, stop the consumer and bail out
                Completed.Set();
                Stop();
                return;
            }

            TaskCompletionSource<bool> eventing = new TaskCompletionSource<bool>();
            call.OnFaxError += (a, c, e, p) =>
            {
                Logger.LogError("Actual fax receive had an error");
                eventing.SetResult(true);
            };
            call.OnFaxFinished += (a, c, e, p) =>
            {
                var settings = p.Fax.ParametersAs<CallingEventParams.FaxParams.FaxSettings.FinishedSettings>();
                if (settings.Success)
                {
                    Successful = true;
                }
                else
                {
                    Logger.LogError("Actual fax receive had an issue: {0}", settings.ResultText);
                }
                eventing.SetResult(true);
            };

            FaxResult receiveResult = call.FaxReceive();

            if (!receiveResult.Successful)
            {
                Successful = false;
                Logger.LogError("Receive fax was unsuccessful");
                eventing.SetResult(true);
            }
            else
            {
                eventing.Task.Wait();
                if (!Successful)
                {
                    Logger.LogError("Fax receive did not give a successful finished event");
                }
            }

            Completed.Set();
        }
    }

    internal class Program
    {
        private static ILogger Logger { get; set; }

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
            string host = Environment.GetEnvironmentVariable("TEST_HOST");
            string project = Environment.GetEnvironmentVariable("TEST_PROJECT");
            string token = Environment.GetEnvironmentVariable("TEST_TOKEN");
            string context = Environment.GetEnvironmentVariable("TEST_CONTEXT");
            string document = Environment.GetEnvironmentVariable("TEST_DOCUMENT");
            string toNumber = Environment.GetEnvironmentVariable("TEST_TO_NUMBER");
            string fromNumber = Environment.GetEnvironmentVariable("TEST_FROM_NUMBER");

            // Make sure we have mandatory options filled in
            if (host == null)
            {
                Logger.LogError("Missing 'TEST_HOST' environment variable");
                return -1;
            }
            if (project == null)
            {
                Logger.LogError("Missing 'TEST_PROJECT' environment variable");
                return -1;
            }
            if (token == null)
            {
                Logger.LogError("Missing 'TEST_TOKEN' environment variable");
                return -1;
            }
            if (context == null)
            {
                Logger.LogError("Missing 'TEST_CONTEXT' environment variable");
                return -1;
            }
            if (document == null)
            {
                Logger.LogError("Missing 'TEST_DOCUMENT' environment variable");
                return -1;
            }
            if (toNumber == null)
            {
                Logger.LogError("Missing 'TEST_TO_NUMBER' environment variable");
                return -1;
            }
            if (fromNumber == null)
            {
                Logger.LogError("Missing 'TEST_FROM_NUMBER' environment variable");
                return -1;
            }

            // Create the TestConsumer
            TestConsumer consumer = new TestConsumer(host, project, token, context, document, toNumber, fromNumber);

            // Run a backgrounded task that will stop the consumer after 2 minutes
            Task.Run(() =>
            {
                // Wait more than long enough for the test to be completed
                if (!consumer.Completed.Wait(TimeSpan.FromMinutes(2))) Logger.LogError("Test timed out");
                consumer.Stop();
            });

            try
            {
                // Run the TestConsumer which blocks until it is stopped
                consumer.Run();
            }
            catch (Exception exc)
            {
                Logger.LogError(exc, "Consumer run exception");
            }

            timer.Stop();

            // Report test outcome
            if (!consumer.Successful) Logger.LogError("Completed unsuccessfully: {0} elapsed", timer.Elapsed);
            else Logger.LogInformation("Completed successfully: {0} elapsed", timer.Elapsed);

#if DEBUG
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
#endif
            return consumer.Successful ? 0 : -1;
        }
    }
}
