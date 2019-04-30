using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class PlaySilenceAndCollectAction
    {
        internal PlaySilenceAndCollectAction(PlayMediaAndCollectAction action)
        {
            mAction = action;
        }

        private readonly PlayMediaAndCollectAction mAction;

        public Call Call { get { return mAction.Call; } }
        public string ControlID { get { return mAction.ControlID; } }
        public CallMedia Media { get { return mAction.Media[0]; } }
        public CallCollect Collect { get { return mAction.Collect; } }

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
