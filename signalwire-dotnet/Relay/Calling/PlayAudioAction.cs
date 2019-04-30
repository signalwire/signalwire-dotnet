using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class PlayAudioAction
    {
        internal PlayAudioAction(PlayMediaAction action)
        {
            mAction = action;
        }

        private readonly PlayMediaAction mAction;

        public Call Call { get { return mAction.Call; } }
        public string ControlID { get { return mAction.ControlID; } }
        public CallMedia Media { get { return mAction.Media[0]; } }

        public void Stop()
        {
            StopAsync().Wait();
        }

        public async Task StopAsync()
        {
            await mAction.StopAsync();
        }
    }
}
