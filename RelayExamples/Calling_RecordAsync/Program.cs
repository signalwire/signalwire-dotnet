using SignalWire.Relay;
using SignalWire.Relay.Calling;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Calling_RecordAsync
{
    internal class TestConsumer : Consumer
    {
        protected override void Setup()
        {
            Project = "XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX";
            Token = "PTXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";
            Contexts = new List<string> { "test" };
        }

        // This is executed in a new thread each time, so it is safe to use blocking calls
        protected override void OnIncomingCall(Call call)
        {
            // Answer the incoming call, block until it's answered or an error occurs
            AnswerResult resultAnswer = call.Answer();

            if (!resultAnswer.Successful)
            {
                // The call was not answered successfully, stop the consumer and bail out
                Stop();
                return;
            }

            // Record the call, do not block
            RecordAction actionRecord = call.RecordAsync(new CallRecord
            {
                Audio = new CallRecord.AudioParams
                {
                    Direction = CallRecord.AudioParams.AudioDirection.both,
                    InitialTimeout = 5,
                    EndSilenceTimeout = 5,
                }
            });

            // Play some TTS to be captured in the recording with whatever the end user says
            call.PlayTTS("Welcome to SignalWire!");

            // If the recording doesn't hear anything for a few seconds it will cause the recording to complete
            while (!actionRecord.Completed)
            {
                // Wait for the async recording to complete, you can do other things here
                Thread.Sleep(1000);
            }

            Console.WriteLine("The recording was {0}", actionRecord.Result.Successful ? "Successful" : "Unsuccessful");
            if (actionRecord.Result.Successful)
            {
                Console.WriteLine("You can find the {0} duration recording at {1}", TimeSpan.FromSeconds(actionRecord.Result.Duration.Value).ToString(), actionRecord.Result.Url);
            }

            // Hangup
            call.Hangup();

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