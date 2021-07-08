using Blade;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignalWire.Relay;
using SignalWire.Relay.Testing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Testing_Timeout
{
    internal class TestConsumer : Consumer
    {
        private ILogger Logger { get; } = SignalWireLogging.CreateLogger<TestConsumer>();

        internal ManualResetEventSlim Completed { get; } = new ManualResetEventSlim();

        internal bool Successful { get; private set; } = false;

        protected override void Setup()
        {
            Host = Environment.GetEnvironmentVariable("TEST_HOST");
            Project = Environment.GetEnvironmentVariable("TEST_PROJECT");
            Token = Environment.GetEnvironmentVariable("TEST_TOKEN");

            if (string.IsNullOrWhiteSpace(Host))
            {
                Logger.LogError("Missing 'TEST_HOST' environment variable");
                throw new ArgumentNullException("Host");
            }
            if (string.IsNullOrWhiteSpace(Project))
            {
                Logger.LogError("Missing 'TEST_PROJECT' environment variable");
                throw new ArgumentNullException("Project");
            }
            if (string.IsNullOrWhiteSpace(Token))
            {
                Logger.LogError("Missing 'TEST_TOKEN' environment variable");
                throw new ArgumentNullException("Token");
            }
        }

        // This is executed in a new thread each time, so it is safe to use blocking calls
        protected override void Ready()
        {
            TimeoutRetryResult resultTimeout = Client.Testing.TimeoutRetry(Request.DEFAULT_RESPONSE_TIMEOUT_SECONDS * 3);

            if (!resultTimeout.Successful)
            {
                Logger.LogError("Timeout was unsuccessful");
                Completed.Set();
                return;
            }

            Successful = true;
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

            // Create the TestConsumer
            TestConsumer consumer = null;
            try { consumer = new TestConsumer(); }
            catch(Exception) { return -1; }

            // Run a backgrounded task that will stop the consumer after 1 minute
            Task.Run(() =>
            {
                // Wait more than long enough for the test to be completed
                if (!consumer.Completed.Wait(TimeSpan.FromMinutes(1))) Logger.LogError("Test timed out");
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
