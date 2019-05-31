using Blade;
using Microsoft.Extensions.Logging;
using SignalWire.Relay;
using SignalWire.Relay.Calling;
using System;
using System.Threading.Tasks;

namespace Calling_OutboundCall
{
    internal class Program
    {
        private static ILogger Logger = null;

        public static void Main()
        {
            // Add a logging provider
            SignalWireLogging.LoggerFactory.AddSimpleConsole(LogLevel.Information);

            // Create a logger for our own use tied to the existing providers
            Logger = SignalWireLogging.CreateLogger<Program>();

            // Create and configure the client with the host, project, and token
            using (Client client = new Client("XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX",
                                              "PTXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
                                              host: "relay.signalwire.com"))
            {
                // Assign the ready event with a callback
                client.OnReady += OnReady;

                // Assign the disconnected event with a callback
                client.OnDisconnected += OnDisconnected;

                // Start the client
                client.Connect();

                // Prevent exit until a key is pressed, do not press a key or the application will exit immediately
                Console.ReadKey(true);
            }
        }

        private static void OnReady(Client client)
        {
            Logger.LogInformation("Your client is ready!");

            // Create a new call, this does not start the call
            PhoneCall call = client.Calling.NewPhoneCall("+1XXXXXXXXXX", "+1YYYYYYYYYY");

            // Hook the specific event callbacks for call answered and call ended state changes
            call.OnAnswered += Call_OnAnswered;
            call.OnEnded += Call_OnEnded;

            // Event callbacks such as this OnReady must not block waiting for messages like Begin so we must
            // operate in a new thread, so simply create a new task that executes from the task thread pool
            Task.Run(() => {
                // Request the call be started, which will block until a server responds that it has accepted
                // the request or throw an exception if there is any errors
                try { call.Begin(); }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Exception from Call.Begin()");
                    return;
                }
                // At this point the OnAnswered callback will be called when the call is answered or the
                // OnEnded callback will be called when the call is terminated even if not answered
                Logger.LogInformation("Waiting for call to be answered");
            });
        }

        private static void OnDisconnected(Client client)
        {
            // The client has disconnected either due to a shutdown from disposing the client, or some failure
            // at runtime in which case it will attempt to reconnect itself
            Logger.LogInformation("Your client has disconnected!");
        }

        private static void Call_OnAnswered(CallingAPI api,
                                            Call call,
                                            CallState oldState,
                                            CallEventParams.StateParams stateParams)
        {
            Logger.LogInformation("OnAnswered: {0}", call.CallID);

            // Hook the event callbacks for call play and call collect state changes
            call.OnPlayStateChange += Call_OnPlayStateChange;
            call.OnCollect += Call_OnCollect;

            // Event callbacks such as this OnAnswered must not block waiting for messages like PlayTTSAndCollect so we must
            // operate in a new thread, so simply create a new task that executes from the task thread pool
            Task.Run(() =>
            {
                // Request the call have tts played to it and collect digit input, which will block until a server responds
                // that it has accepted the request to play the audio and has begun collecting for the call or throw an
                // exception if there is any errors
                try
                {
                    call.PlayTTSAndCollect(new CallMedia.TTSParams()
                    {
                        Text = "Welcome to SignalWire!",
                    },
                        new CallCollect()
                        {
                            InitialTimeout = 10,
                            Digits = new CallCollect.DigitsParams()
                            {
                                Max = 3,
                                DigitTimeout = 5
                            }
                        });

                }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Exception from Call.PlayAudio()");
                    return;
                }
                // At this point the OnCollect callback will be called when the call collect state changes or
                // the OnEnded callback will be called when the call is hung up
                Logger.LogInformation("Playing TTS to the call and waiting for collected digits");
            });
        }

        private static void Call_OnEnded(CallingAPI api,
                                         Call call,
                                         CallState oldState,
                                         CallEventParams.StateParams stateParams)
        {
            Logger.LogInformation("OnAnswered: {0}", call.CallID);
        }

        private static void Call_OnPlayStateChange(CallingAPI api, Call call, CallEventParams.PlayParams playParams)
        {
            Logger.LogInformation("OnPlayStateChange: {0}, {1}", call.CallID, playParams.State);
        }

        private static void Call_OnCollect(CallingAPI api, Call call, CallEventParams.CollectParams collectParams)
        {
            Logger.LogInformation("OnCollect: {0}, {1}", call.CallID, collectParams.Result.Type);

            // Only log some added output if it was the expected collect result of digit
            if (collectParams.Result.Type == CallEventParams.CollectParams.ResultParams.ResultType.digit)
            {
                CallEventParams.CollectParams.ResultParams.DigitParams digitParams = null;

                // Try to parse the parameters specific to digit collection results
                try
                {
                    digitParams = collectParams.Result.ParametersAs<CallEventParams.CollectParams.ResultParams.DigitParams>();
                }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Exception from ParametersAs()");
                    return;
                }
                // At this point the OnEnded callback will be called when the call is hung up
                Logger.LogInformation("Result: {0}, {1}", digitParams.Digits, digitParams.Terminator);
            }
        }
    }
}
