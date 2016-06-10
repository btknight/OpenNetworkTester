using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NetworkFlooder
{
    /// <summary>
    /// Monitored by FlooderThreads to figure out what size packets to send, where to send them, and how fast to send.
    /// Also contains some amount of static running state.  These variables are checked directly by FlooderThreads during operation,
    /// so operating state can be adjusted dynamically.
    /// </summary>
    public class FlooderAction
    {
        /// <summary>
        /// Number of concurrent sender threads.  Sender threads do most of the work.  Keep in mind that sender threads 
        /// spawn receiver threads, so the number of threads working on sockets is equal to Concurrency * 2.
        /// </summary>
        public int Concurrency { get; private set; }
        
        /// <summary>
        /// Target to test.
        /// </summary>
        public IPAddress TargetIP { get; set; }
        
        /// <summary>
        /// Destination UDP port.
        /// </summary>
        public int DestPort { get; set; }
        
        /// <summary>
        /// Amount of data to send per second, expressed in octets.
        /// </summary>
        public int BandwidthOctets { get; set; }
        
        /// <summary>
        /// Amount of data to send per socket Send() call, inclusive of IP headers.
        /// </summary>
        public int PacketSize { get; set; }

        /// <summary>
        /// The number of processor ticks in one second.
        /// </summary>
        public static long TicksPerSecond { get; private set; }
        
        /// <summary>
        /// Amount of time that sender and receiver threads will operate before sending a FlooderResult back to the Flooder object, 
        /// and re-checking this object for changes.  The main thread loop will stop sending data and re-initialize based on any changed
        /// variables.
        /// </summary>
        public int TimeQuantum { get; private set; }

        public FlooderAction()
        {
            TargetIP = null;
            Concurrency = (Environment.ProcessorCount / 4 > 0) ? Environment.ProcessorCount / 4 : 1;
            //Concurrency = 1;
            DestPort = 0;
            BandwidthOctets = 0;
            PacketSize = 0;
            TimeQuantum = 250;
        }

        static FlooderAction()
        {
            Task t = Task.Run(() => { FindTicksPerMillisecond(); });
        }

        /// <summary>
        /// If you read Microsoft's documentation on processor ticks, you will learn that 1 tick is equal to 
        /// 100 nanoseconds, or 0.1 microsecond. This is a damn dirty lie.  This loop uses the Stopwatch class
        /// to figure out that number.
        /// </summary>
        private static void FindTicksPerMillisecond()
        {
            Stopwatch SW = new Stopwatch();
            Debug.WriteLine("Calibrating delay...");
            DateTime StartTime = DateTime.Now;
            SW.Start();
            while (SW.ElapsedMilliseconds < 1000) { }
            SW.Stop();
            TicksPerSecond = SW.ElapsedTicks;
            Debug.WriteLine("Ticks per second: " + TicksPerSecond.ToString());
        }
    }
}
