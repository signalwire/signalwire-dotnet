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
            Token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzUxMiJ9.eyJpYXQiOjE2MDYxNTUwNjQsImlzcyI6IlNpZ25hbFdpcmUgSldUIiwianRpIjoiU0ZRRDdUUWxVZGxremhSMlhBTk8tQm0xZk5RIiwic2NvcGUiOiJ3ZWJydGMiLCJzdWIiOiIwZWIzODlhYy1lYzUwLTRjNDQtOWRhOC04NTk0ZjFkMTU5M2MiLCJyZXNvdXJjZSI6IjA2NTMyOWYyLWQ1MGMtNDA0Ni04OTE5LTMyMDdjNGVjMTA5YSIsImV4cCI6MTYwNjE1NTk2NH0.7PQ9gI6qKcQqB1od8VY50fKeo4JzsUJ_r9QtQwwXex2sq6cWfxfFqOEnnUGLMS0RwJInpCjCLnvMc9aIp95-8w";
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