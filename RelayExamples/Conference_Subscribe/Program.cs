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
        private const string CHANNEL = "conference-list";

        protected override void Setup()
        {
            Host = "relay.swire.io";
            Project = "0eb389ac-ec50-4c44-9da8-8594f1d1593c";
            Token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzUxMiJ9.eyJpYXQiOjE2MDYxNjQzNTYsImlzcyI6IlNpZ25hbFdpcmUgSldUIiwianRpIjoiXzlaZlBQZl9PVGVtQ0pYSk5xOThnN3hUSnBZIiwic2NvcGUiOiJ3ZWJydGMiLCJzdWIiOiIwZWIzODlhYy1lYzUwLTRjNDQtOWRhOC04NTk0ZjFkMTU5M2MiLCJyZXNvdXJjZSI6ImYwNTMxNDkyLTVkZGEtNDA3Zi05ZTQ1LTM1YmI4MTU4MzI1YiIsImV4cCI6MTYwNjE2NTI1Nn0.qikNirlkwU75CGRktFvM_Kycfjx8XHAtrMIsZCG7BZOcI-4Vok92xNUby9TUDB5hQgqy8pwp_wFP6VVWQVRAyQ";
            JWT = true;
        }

        protected override void Ready()
        {
            SubscribeResult resultSubscribe = Client.Conferencing.Subscribe(null, new List<string> { CHANNEL });

            if (!resultSubscribe.Successful)
            {
                Stop();
                return;
            }

            Thread.Sleep(5000);

            ListBootstrapResult resultListBootstrap = Client.Conferencing.ListBootstrap();

            if (!resultListBootstrap.Successful)
            {
                Stop();
                return;
            }

            Thread.Sleep(5000);

            //UnsubscribeResult resultUnsubscribe = Client.Conferencing.Unsubscribe(null, new List<string> { CHANNEL });

            //if (!resultSubscribe.Successful)
            //{
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