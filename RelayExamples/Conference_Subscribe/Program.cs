using SignalWire.Relay;
using SignalWire.Relay.Conferencing;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Conference_Subscribe
{
    internal class TestConsumer : Consumer
    {
        protected override void Setup()
        {
            Project = "XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX";
            Token = "PTXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";
            //Contexts = new List<string> { "test" };
        }

        protected override void Ready()
        {
            // Create a new phone call and dial it immediately, block until it's answered, times out, busy, or another error occurs
            SubscribeResult resultSubscribe = Client.Conferencing.Subscribe("test", new List<string> { "info" });

            if (!resultSubscribe.Successful)
            {
                // The conference was not subscribed to successfully, stop the consumer and bail out
                Stop();
                return;
            }

            // Stop the consumer
            Stop();
        }
    }

    internal class Program
    {
        public static void Main()
        {
            // Create the TestConsumer and run it
            new TestConsumer().Run();

            // Prevent exit until a key is pressed
            Console.Write("Press any key to exit...");
            Console.ReadKey(true);
        }
    }
}