using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkFlooder
{
    /// <summary>
    /// Contains results from FlooderThread runs.
    /// </summary>
    public class FlooderResult
    {
        /// <summary>
        /// The first moment from whence data is recorded.
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// The last moment when the last data point was captured.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// The interval between EndTime and StartTime expressed in milliseconds.
        /// </summary>
        public int Interval { get { return (int)(EndTime - StartTime).TotalMilliseconds; } }
        
        /// <summary>
        /// The sum of packet latency information, captured in terms of processor ticks.
        /// </summary>
        public long SumOfLatency { get; set; }
        
        /// <summary>
        /// Number of packets sent.
        /// </summary>
        public int PacketsSent { get; set; }

        /// <summary>
        /// Number of packets known to be lost.
        /// </summary>
        public Nullable<int> PacketsLost { get; set; }

        /// <summary>
        /// Last sequence number observed by the Receive() thread.
        /// </summary>
        public Nullable<ulong> SeqLastSeen { get; set; }

        /// <summary>
        /// Total number of octets sent.
        /// </summary>
        public int OctetsSent { get; set; }
        
        /// <summary>
        /// Number of octets sent per second.
        /// </summary>
        public int OctetsSentPerSecond
        {
            get
            {
                if (Interval == 0) { return 0; }
                return (int)((long)OctetsSent * 1000 / (long)Interval);
            }
        }

        /// <summary>
        /// Total number of octets received back.
        /// </summary>
        public int OctetsReceived { get; set; }
        
        /// <summary>
        /// Number of packets received.
        /// </summary>
        public int PacketsReceived { get; set; }
        
        /// <summary>
        /// Number of octets received per second.
        /// </summary>
        public int OctetsReceivedPerSecond
        {
            get
            {
                if (Interval == 0) { return 0; }
                return (int)((long)OctetsReceived * 1000 / (long)Interval);
            }
        }

        public FlooderResult()
        {
            StartTime = DateTime.Now;
            EndTime = StartTime;
            OctetsSent = OctetsReceived = 0;
            PacketsSent = PacketsReceived = 0;
            SumOfLatency = 0;
            PacketsLost = null;
            SeqLastSeen = null;
        }

        /// <summary>
        /// Adds two FlooderResult objects together.
        /// </summary>
        /// <param name="fr1">FlooderResult object #1</param>
        /// <param name="fr2">FlooderResult object #2</param>
        /// <returns>Combined FlooderResult object</returns>
        public static FlooderResult operator +(FlooderResult fr1, FlooderResult fr2)
        {
            var fr = new FlooderResult();
            fr.StartTime = fr1.StartTime < fr2.StartTime ? fr1.StartTime : fr2.StartTime;
            fr.EndTime = fr1.EndTime > fr2.EndTime ? fr1.EndTime : fr2.EndTime;
            fr.OctetsSent = fr1.OctetsSent + fr2.OctetsSent;
            fr.PacketsSent = fr1.PacketsSent + fr2.PacketsSent;
            fr.OctetsReceived = fr1.OctetsReceived + fr2.OctetsReceived;
            fr.PacketsReceived = fr1.PacketsReceived + fr2.PacketsReceived;
            fr.SumOfLatency = fr1.SumOfLatency + fr2.SumOfLatency;
            if (fr1.PacketsReceived > 0)
            {
                fr.PacketsLost = fr1.PacketsLost;
            }
            if (fr2.PacketsReceived > 0)
            {
                fr.PacketsLost = (fr.PacketsLost.HasValue) ? fr.PacketsLost + fr2.PacketsLost : fr2.PacketsLost;
            }
            fr.SeqLastSeen = (fr1.SeqLastSeen > fr2.SeqLastSeen) ? fr1.SeqLastSeen : fr2.SeqLastSeen;
            return fr;
        }
    }
}
