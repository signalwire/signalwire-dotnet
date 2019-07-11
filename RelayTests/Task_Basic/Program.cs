using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignalWire.Relay;
using SignalWire.Relay.Calling;
using SignalWire.Relay.Tasking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Task_Basic
{
    internal class TestConsumer : Consumer
    {
        internal TestConsumer(string host, string space, string project, string token, string context)
        {
            Host = host;
            Space = space;
            Project = project;
            Token = token;
            Contexts = new List<string> { context };
        }

        private ILogger Logger { get; } = SignalWireLogging.CreateLogger<TestConsumer>();

        internal ManualResetEventSlim Completed { get; } = new ManualResetEventSlim();

        internal bool Successful { get; private set; } = false;

        protected override void Ready()
        {
            Client.Tasking.Deliver(Contexts[0], new JObject { ["foo"] = 123 });
        }

        // This is executed in a new thread each time, so it is safe to use blocking calls
        protected override void OnTask(TaskingEventParams eventParams)
        {
            Logger.LogInformation("Received task successfully!\n{0}", eventParams.Message.ToString(Formatting.Indented));

            Successful = eventParams.Message != null && eventParams.Message.Value<int>("foo") == 123;
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
            string space = Environment.GetEnvironmentVariable("TEST_SPACE");
            string project = Environment.GetEnvironmentVariable("TEST_PROJECT");
            string token = Environment.GetEnvironmentVariable("TEST_TOKEN");
            string context = Environment.GetEnvironmentVariable("TEST_CONTEXT");

            // Make sure we have mandatory options filled in
            if (host == null)
            {
                Logger.LogError("Missing 'TEST_HOST' environment variable");
                return -1;
            }
            if (space == null)
            {
                Logger.LogError("Missing 'TEST_SPACE' environment variable");
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

            // Create the TestConsumer
            TestConsumer consumer = new TestConsumer(host, space, project, token, context);

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
