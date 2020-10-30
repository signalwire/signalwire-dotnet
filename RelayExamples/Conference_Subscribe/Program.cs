using Blade;
using Microsoft.Extensions.Logging;
using SignalWire.Relay;
using SignalWire.Relay.Conferencing;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Conference_Subscribe
{
    internal class TestConsumer : Consumer
    {
        private const string CONFERENCE = "conf:3472";
        private const string CHANNEL = "conference-liveArray";

        protected override void Setup()
        {
            Host = "relay.swire.io";
            Project = "0eb389ac-ec50-4c44-9da8-8594f1d1593c";
            Token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzUxMiJ9.eyJpYXQiOjE2MDI4ODA4NzAsImlzcyI6IlNpZ25hbFdpcmUgSldUIiwianRpIjoid2Q1VGZ0eTlpLUhNMjIyMlBuRlM2WFRVNjk0Iiwic2NvcGUiOiJ3ZWJydGMiLCJzdWIiOiIwZWIzODlhYy1lYzUwLTRjNDQtOWRhOC04NTk0ZjFkMTU5M2MiLCJyZXNvdXJjZSI6ImJhM2MyYWYyLTMwN2YtNGI3ZC05ZDc0LWQ5ZjFjZDljNDYzYyIsImV4cCI6MTYwMjg4MTc3MH0.aCnDdyu5smCA-ZJTVF3LxdbWG5XFXzuId-Twkz2BGa2wT_V2AwPBLxspBFYtg1refEG40UnSjO25rqYRx3lSPA";
            //Contexts = new List<string> { "test" };
            JWT = true;
        }

        protected override void Ready()
        {
            // Create a new phone call and dial it immediately, block until it's answered, times out, busy, or another error occurs
            SubscribeResult resultSubscribe = Client.Conferencing.Subscribe(CONFERENCE, new List<string> { CHANNEL });

            if (!resultSubscribe.Successful)
            {
                // The conference was not subscribed to successfully, stop the consumer and bail out
                Stop();
                return;
            }

            Thread.Sleep(10000);

            //UnsubscribeResult resultUnsubscribe = Client.Conferencing.Unsubscribe(CONFERENCE, new List<string> { CHANNEL });

            //if (!resultSubscribe.Successful)
            //{
            //    // The conference was not unsubscribed from successfully, stop the consumer and bail out
            //    Stop();
            //    return;
            //}

            // Stop the consumer
            Stop();
        }
    }

    internal class Program
    {
        public static void Main()
        {
            BladeLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);
            SignalWireLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);

            // Create the TestConsumer and run it
            new TestConsumer().Run();

            // Prevent exit until a key is pressed
            Console.Write("Press any key to exit...");
            Console.ReadKey(true);
        }
    }
}