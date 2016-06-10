using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetworkFlooder;

namespace ONT
{
    public class FlooderResultVM : FlooderResult
    {
        /// <summary>
        /// Total latency expressed as fractions of a second.
        /// </summary>
        public double Latency
        {
            get
            {
                double lat = (double)SumOfLatency / (double)PacketsReceived;
                lat /= (double)FlooderAction.TicksPerSecond;
                return lat;
            }
        }
        
        public FlooderResultVM(FlooderResult r, int wanFrameSize)
        {
            this.StartTime = r.StartTime;
            this.EndTime = r.EndTime;
            this.SumOfLatency = r.SumOfLatency;
            this.PacketsSent = r.PacketsSent;
            this.PacketsLost = r.PacketsLost;
            this.SeqLastSeen = r.SeqLastSeen;
            this.OctetsSent = r.OctetsSent + (wanFrameSize * r.PacketsSent);
            this.OctetsReceived = r.OctetsReceived + (wanFrameSize * r.PacketsReceived);
            this.PacketsReceived = r.PacketsReceived;
        }
    }
}
