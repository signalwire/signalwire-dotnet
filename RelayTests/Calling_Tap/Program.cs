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

namespace Calling_Tap
{
    internal class TestConsumer : Consumer
    {
        private string RTPAddr { get; set; }
        private int RTPPort { get; set; }

        internal TestConsumer(string host, string project, string token, string context, string rtpAddr, int rtpPort)
        {
            Host = host;
            Project = project;
            Token = token;
            Contexts = new List<string> { context };
            RTPAddr = rtpAddr;
            RTPPort = rtpPort;
        }

        private ILogger Logger { get; } = SignalWireLogging.CreateLogger<TestConsumer>();

        internal ManualResetEventSlim Completed { get; } = new ManualResetEventSlim();

        internal bool Successful { get; private set; } = false;

        // This is executed in a new thread each time, so it is safe to use blocking calls
        protected override void OnIncomingCall(Call call)
        {
            AnswerResult resultAnswer = call.Answer();

            if (!resultAnswer.Successful)
            {
                Completed.Set();
                return;
            }

            UdpClient udpClient = new UdpClient(RTPPort);

            TapAction actionTap = call.TapAsync(
                new CallTap
                {
                    Type = CallTap.TapType.audio,
                    Parameters = new CallTap.AudioParams
                    {
                        Direction = CallTap.AudioParams.AudioDirection.both,
                    }
                },
                new CallTapDevice
                {
                    Type = CallTapDevice.DeviceType.rtp,
                    Parameters = new CallTapDevice.RTPParams
                    {
                        Address = RTPAddr,
                        Port = RTPPort,
                    }
                }
                );

            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, RTPPort);
            byte[] data = udpClient.Receive(ref ipEndPoint);
            Task<UdpReceiveResult> resultReceive = udpClient.ReceiveAsync();

            bool gotData = false;
            gotData = resultReceive.Wait(5000);

            if (gotData) Logger.LogInformation("Received {0} RTP bytes", data.Length);

            actionTap.Stop();

            HangupResult resultHangup = call.Hangup();

            Successful = gotData && actionTap.Result.Successful && resultHangup.Successful;
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
            string rtpAddr = Environment.GetEnvironmentVariable("TEST_RTPADDR");
            string rtpPort = Environment.GetEnvironmentVariable("TEST_RTPPORT");

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
            if (rtpAddr == null)
            {
                Logger.LogError("Missing 'TEST_RTPADDR' environment variable");
                return -1;
            }
            if (rtpPort == null)
            {
                Logger.LogError("Missing 'TEST_RTPPORT' environment variable");
                return -1;
            }

            // Create the TestConsumer
            TestConsumer consumer = new TestConsumer(host, project, token, context, rtpAddr, int.Parse(rtpPort));

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
