using Blade;
using Microsoft.Extensions.Logging;
using SignalWire.Relay;
using SignalWire.Relay.Calling;
using System;
using System.Collections.Generic;

namespace Calling_PlayConnectPlay
{
    internal class TestConsumer : Consumer
    {
        protected override void Setup()
        {
            // Setup logging to console for Blade and SignalWire
            BladeLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);
            SignalWireLogging.LoggerFactory.AddSimpleConsole(LogLevel.Trace);

            Project = "XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX";
            Token = "PTXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";
            Contexts = new List<string> { "test" };
        }

        // This is executed in a new thread each time, so it is safe to use blocking calls
        protected override void OnIncomingCall(Call call)
        {
            Console.WriteLine("Answering");

            // Answer the incoming call, block until it's answered or an error occurs
            AnswerResult resultAnswer = call.Answer();

            if (!resultAnswer.Successful)
            {
                // The call was not answered successfully, stop the consumer and bail out
                Stop();
                return;
            }

            call.PlayTTS("Welcome to SignalWire!");

            Console.WriteLine("Connecting");

            // Connect the inbound call to an outbound call
            ConnectResult resultConnect = call.Connect(new List<List<CallDevice>>
            {
                new List<CallDevice>
                {
                    new CallDevice
                    {
                        Type = CallDevice.DeviceType.phone,
                        Parameters = new CallDevice.PhoneParams
                        {
                            ToNumber = "+1555XXXXXXX",
                            FromNumber = "+1555XXXXXXX",
                        }
                    }
                }
            });

            if (resultConnect.Successful)
            {
                Console.WriteLine("Connected");
                resultConnect.Call.PlayTTS("Welcome!");

                // Wait upto 15 seconds for the connected side to hangup
                if (!resultConnect.Call.WaitForEnded()) //TimeSpan.FromSeconds(15)))
                {
                    // If the other side of the outbound call doesn't hang up, then hang it up
                    resultConnect.Call.Hangup();
                }
            }

            call.PlayTTS("Thank you for trying SignalWire!");

            // Hangup the inbound call
            call.Hangup();

            // Stop the consumer
            Stop();
        }
    }

    internal class Program
    {
        public static void Main(string[] args)
        {
            // Create the TestConsumer and run it
            new TestConsumer().Run();

            // Prevent exit until a key is pressed, do not press a key or the application will exit immediately
            Console.Write("Press any key to exit...");
            Console.ReadKey(true);
        }
    }
}
