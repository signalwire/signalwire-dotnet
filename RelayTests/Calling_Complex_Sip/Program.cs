using Blade;
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

namespace Calling_Complex_Sip
{
    /* This complete test requires 2 phone numbers on signalwire, one bound as an inbound DID to relay
     * and the other used for outbound from number validation.
     * It will first Dial to the inbound DID as Call1-A.
     * This will be received and answered as Call1-B.
     * Then it will Connect to the inbound DID as Call2-A.
     * This will also be received and answered as Call2-B.
     * Upon seeing Call1-A and Call2-A connected as peers, the test succeeds.
     */
    internal class TestConsumer : Consumer
    {
        private ILogger Logger { get; } = SignalWireLogging.CreateLogger<TestConsumer>();

        internal ManualResetEventSlim Completed { get; } = new ManualResetEventSlim();

        internal bool Successful { get; private set; } = false;

        private string ToNumber { get; set; }

        private string FromNumber { get; set; }
        private string ToSip { get; set; }

        private string FromSip { get; set; }

        protected override void Setup()
        {
            Host = Environment.GetEnvironmentVariable("TEST_HOST");
            Project = Environment.GetEnvironmentVariable("TEST_PROJECT");
            Token = Environment.GetEnvironmentVariable("TEST_TOKEN");
            Contexts = new List<string> { Environment.GetEnvironmentVariable("TEST_CONTEXT") };
            ToNumber = Environment.GetEnvironmentVariable("TEST_TO_NUMBER");
            FromNumber = Environment.GetEnvironmentVariable("TEST_FROM_NUMBER");
            ToSip = Environment.GetEnvironmentVariable("TEST_TO_SIP");
            FromSip = Environment.GetEnvironmentVariable("TEST_FROM_SIP");

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
            if (string.IsNullOrWhiteSpace(Contexts[0]))
            {
                Logger.LogError("Missing 'TEST_CONTEXT' environment variable");
                throw new ArgumentNullException("Context");
            }
            if (string.IsNullOrWhiteSpace(ToNumber))
            {
                Logger.LogError("Missing 'TEST_TO_NUMBER' environment variable");
                throw new ArgumentNullException("ToNumber");
            }
            if (string.IsNullOrWhiteSpace(FromNumber))
            {
                Logger.LogError("Missing 'TEST_FROM_NUMBER' environment variable");
                throw new ArgumentNullException("FromNumber");
            }
        }

        // This is executed in a new thread each time, so it is safe to use blocking calls
        protected override void Ready()
        {
            // Create the first outbound call leg to the inbound DID associated to the context this client is receiving
            // This will block until the call is answered, times out, busy, or an error occurred
            DialResult resultDial = Client.Calling.DialSip(ToNumber, FromNumber);

            if (!resultDial.Successful)
            {
                Logger.LogError("Call1 was not answered");
                Completed.Set();
                return;
            }

            // The call was answered, try to connect another outbound call to it
            // The top level list of the devices represents entries that will be called in serial,
            // one at a time.  The inner list of devices represents a set of devices to call in
            // parallel with each other.  Ultimately only one device wins by answering first.
            ConnectResult resultConnect = resultDial.Call.Connect(new List<List<CallDevice>>
                {
                    new List<CallDevice>
                    {
                        new CallDevice
                        {
                            Type = CallDevice.DeviceType.phone,
                            Parameters = new CallDevice.PhoneParams
                            {
                                ToNumber = ToNumber,
                                FromNumber = FromNumber,
                            }
                        }
                    }
                });

            if (!resultConnect.Successful)
            {
                Logger.LogError("Call2 was not connected");
            }

            // Hangup both calls
            resultConnect.Call?.Hangup();
            resultDial.Call.Hangup();

            // Mark the test successful and terminate
            Successful = resultConnect.Successful;
            Completed.Set();
        }

        // This is executed in a new thread each time, so it is safe to use blocking calls
        protected override void OnIncomingCall(Call call)
        {
            AnswerResult resultAnswer = call.Answer();

            if (!resultAnswer.Successful)
            {
                // If answering fails, test stops
                Completed.Set();
            }
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
