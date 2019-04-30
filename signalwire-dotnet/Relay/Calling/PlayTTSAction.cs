using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class PlayTTSAction
    {
        internal PlayTTSAction(PlayMediaAction action)
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
