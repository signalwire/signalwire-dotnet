using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class RecordAction
    {
        internal Call Call { get; set; }

        public string ControlID { get; internal set; }

        public bool Completed { get; internal set; }

        public RecordResult Result { get; internal set; }

        public CallRecordState State { get; internal set; }

        public CallRecord Payload { get; internal set; }

        public string Url { get; internal set; }

        public StopResult Stop()
        {
            Task<LL_RecordStopResult> taskLLRecordStop = Call.API.LL_RecordStopAsync(new LL_RecordStopParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
            });

            LL_RecordStopResult resultLLRecordStop = taskLLRecordStop.Result;

            return new StopResult()
            {
                Successful = resultLLRecordStop.Code == "200",
            };
        }
    }
}
