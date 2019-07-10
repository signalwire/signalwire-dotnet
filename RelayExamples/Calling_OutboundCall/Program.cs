using SignalWire.Relay;
using SignalWire.Relay.Calling;
using System;

namespace Calling_OutboundCall
{
    internal class TestConsumer : Consumer
    {
        protected override void Setup()
        {
            Project = "XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX";
            Token = "PTXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";
        }

        protected override void Ready()
        {
            // Create a new phone call and dial it immediately, block until it's answered, times out, busy, or another error occurs
            DialResult resultDial = Client.Calling.DialPhone("+1XXXXXXXXXX", "+1YYYYYYYYYY");

            if (!resultDial.Successful)
            {
                // The call was not answered successfully, stop the consumer and bail out
                Stop();
                return;
            }

            // Play an audio file, block until it's finished or an error occurs
            resultDial.Call.PlayAudio("https://cdn.signalwire.com/default-music/welcome.mp3");

            // Hangup
            resultDial.Call.Hangup();

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
