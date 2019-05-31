using Blade;
using Microsoft.Extensions.Logging;
using SignalWire.Relay;
using SignalWire.Relay.Calling;
using System;
using System.Threading.Tasks;

namespace Calling_InboundCall
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

            // Hook the event callback for received calls
            client.Calling.OnCallReceived += Calling_OnCallReceived;

            // Event callbacks such as this OnReady must not block waiting for messages like Receive so we must
            // operate in a new thread, so simply create a new task that executes from the task thread pool
            Task.Run(() => {
                // Request the office context to be received, which will block until a server responds that it has
                // accepted the request or throw an exception if there is any errors
                try { client.Calling.Receive("office"); }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Exception from Calling.Receive()");
                    return;
                }
                // At this point the OnCallReceived callback will be called when a call is received
                Logger.LogInformation("Waiting for a received call from the 'office' context");
            });
        }

        private static void OnDisconnected(Client client)
        {
            // The client has disconnected either due to a shutdown from disposing the client, or some failure
            // at runtime in which case it will attempt to reconnect itself
            Logger.LogInformation("Your client has disconnected!");
        }

        private static void Calling_OnCallReceived(CallingAPI api, Call call, CallEventParams.ReceiveParams receiveParams)
        {
            Logger.LogInformation("OnCallReceived: {0}, {1} from context {2}", call.CallID, call.State, receiveParams.Context);

            // Hook the event callback for call state change and more specifically call answered
            call.OnStateChange += Call_OnStateChange;
            call.OnAnswered += Call_OnAnswered;

            // Event callbacks such as this OnCallReceived must not block waiting for messages like Answer so we must
            // operate in a new thread, so simply create a new task that executes from the task thread pool
            Task.Run(() =>
            {
                // Request the call be answered, which will block until a server responds that it has accepted the
                // request to answer the call or throw an exception if there is any errors
                try { call.Answer(); }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Exception from Call.Answer()");
                    return;
                }
                // At this point the OnStateChange callback will be called when the call state changes, including when
                // the call is actually answered, we use OnAnswered for a more specific callback here but you may also
                // transition to ended without being answered which you can capture specifically with OnEnded
                Logger.LogInformation("Answering a received call");
            });
        }

        private static void Call_OnStateChange(CallingAPI api,
                                               Call call,
                                               CallState oldState,
                                               CallEventParams.StateParams stateParams)
        {
            Logger.LogInformation("OnStateChange: {0}, {1}", call.CallID, call.State);
        }

        private static void Call_OnAnswered(CallingAPI api,
                                            Call call,
                                            CallState oldState,
                                            CallEventParams.StateParams stateParams)
        {
            Logger.LogInformation("OnAnswered: {0}", call.CallID);

            // Hook the event callback for call play state change
            call.OnPlayStateChange += Call_OnPlayStateChange;

            // Event callbacks such as this OnAnswered must not block waiting for messages like PlayAudio so we must
            // operate in a new thread, so simply create a new task that executes from the task thread pool
            Task.Run(() =>
            {
                // Request the call have audio played to it, which will block until a server responds that it has accepted the
                // request to play the audio to the call or throw an exception if there is any errors
                try
                {
                    call.PlayAudio(new CallMedia.AudioParams()
                    {
                        URL = "https://cdn.signalwire.com/default-music/welcome.mp3"
                    });
                }
                catch (Exception exc)
                {
                    Logger.LogError(exc, "Exception from Call.PlayAudio()");
                    return;
                }
                // At this point the OnPlayStateChange callback will be called when the call play state changes
                Logger.LogInformation("Playing audio to a call");
            });
        }

        private static void Call_OnPlayStateChange(CallingAPI api, Call call, CallEventParams.PlayParams playParams)
        {
            Logger.LogInformation("OnPlayStateChange: {0}, {1}", call.CallID, playParams.State);
        }
    }
}