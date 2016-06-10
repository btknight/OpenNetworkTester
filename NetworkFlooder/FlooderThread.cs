using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.ComponentModel;
using System.Threading.Tasks;

namespace NetworkFlooder
{
    /// <summary>
    /// Manages UDP send and receive operations.  Monitors a FlooderAction object as continuously-varying input;
    /// returns a FlooderResult object periodically with statistics.
    /// </summary>
    public class FlooderThread : INotifyPropertyChanged
    {
        /// <summary>
        /// Variable holding the FlooderAction object which these threads will monitor.
        /// </summary>
        private FlooderAction Action;

        /// <summary>
        /// The sender thread.
        /// </summary>
        private BackgroundWorker SenderTask;

        /// <summary>
        /// Notifies other objects that a property has changed on this object.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Property holding the latest result from the sender and receiver threads.  Use the PropertyChanged event to subscribe
        /// to these updates.
        /// </summary>
        public FlooderResult LatestResult
        {
            get
            {
                return _latestResult;
            }
            set
            {
                _latestResult = value;
                OnPropertyChanged("LatestResult");
            }
        }
        private FlooderResult _latestResult;

        /// <summary>
        /// True if the threads are actively sending / receiving traffic, false if idle.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return _isRunning;
            }
            protected set
            {
                _isRunning = value;
                OnPropertyChanged("IsRunning");
            }
        }
        private bool _isRunning;

        /// <summary>
        /// The socket object used by the sender / receiver.
        /// </summary>
        Socket UDPSocket;

        /// <summary>
        /// Receive buffer.  Used only by Receive().  Initialized at the object level to avoid overhead of creating and removing a 64k array.
        /// </summary>
        byte[] recvBuffer;
        
        /// <summary>
        /// Header length for the current AddressFamily of the endpoint.  20 + 8 for IPv4, 40 + 8 for IPv6.
        /// </summary>
        int HeaderLen;

        /// <summary>
        /// Timer object used by both threads.  The number of ticks is encoded into the UDP packet for fine-grained latency measurement.
        /// </summary>
        Stopwatch SW;

        /// <summary>
        /// Create new thread manager.
        /// </summary>
        /// <param name="action">FlooderAction object to use as parameters for the sender / receiver</param>
        public FlooderThread(FlooderAction action)
        {
            Action = action;
            UDPSocket = null;
            HeaderLen = 0;
            SW = new Stopwatch();
            recvBuffer = new byte[65535];
            SenderTask = new BackgroundWorker();
            SenderTask.WorkerSupportsCancellation = true;
            SenderTask.WorkerReportsProgress = true;
            SenderTask.DoWork += SenderTask_DoWork;
            SenderTask.ProgressChanged += SenderTask_ProgressChanged;
        }

        /// <summary>
        /// This function handles the FlooderResult object coming back from SenderTask.
        /// </summary>
        /// <param name="sender">this object</param>
        /// <param name="e">Contains the FlooderResult object (e.UserState)</param>
        void SenderTask_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState != null)
            {
                LatestResult = (FlooderResult)e.UserState;
            }
        }
        
        /// <summary>
        /// Event executed on the sender thread that actually starts the sending function.
        /// Called when SenderTask.RunWorkerAsync() is run.
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">ignored</param>
        void SenderTask_DoWork(object sender, DoWorkEventArgs e)
        {
            Sender((BackgroundWorker)sender);
        }

        /// <summary>
        /// Starts the sender.
        /// </summary>
        public void Start()
        {
            SenderTask.RunWorkerAsync();
            //SenderTask = Task.Run(() => { Sender(); }, CancelTask.Token);
        }

        /// <summary>
        /// Stops the sender.
        /// </summary>
        public void Stop()
        {
            SenderTask.CancelAsync();
        }

        /// <summary>
        /// Sends and receives UDP traffic in accordance with the parameters in FlooderAction.
        /// </summary>
        /// <param name="worker">BackgroundWorker object that describes the current thread.
        /// Used to get cancellation state and send back FlooderResult objects.</param>
        private void Sender(BackgroundWorker worker)
        {
            byte[] sendBuffer = null;
            ulong SeqNum = 0;
            Nullable<ulong> SeqLastSeen = null;
            int PacketsToSend = 0;
            long waitInterval = 0;
            Random r = new Random();
            long LargestSlice = 0;
            FlooderResult result;
            TaskFactory<FlooderResult> receiverFactory = new TaskFactory<FlooderResult>();

            IsRunning = true;
            if (UDPSocket == null)
            {
                Debug.WriteLine("Begin Init Socket");
                UDPSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
                UDPSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, 12500000);
                UDPSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 12500000);
                
                // Code to ignore ICMP unreachable messages and prevent the socket from closing on Receive().
                // I don't care if the remote system doesn't reflect messages back.
                //http://stackoverflow.com/questions/7201862/an-existing-connection-was-forcibly-closed-by-the-remote-host
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                UDPSocket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
                
                UDPSocket.Connect(Action.TargetIP, Action.DestPort);
                if (Action.TargetIP.AddressFamily == AddressFamily.InterNetwork)
                    HeaderLen = 28;
                else if (Action.TargetIP.AddressFamily == AddressFamily.InterNetworkV6)
                    HeaderLen = 48;
                else
                    HeaderLen = 0;
                Debug.WriteLine("End Init Socket");
            }
            SW.Start();

            //********** BEGIN Main Loop
            while (!worker.CancellationPending)
            {
                //***** Initialize state for this TimeQuantum.                
                int packetSize = Action.PacketSize;
                int bandwidth = Action.BandwidthOctets;
                long SendStart;
                byte[] SeqBytes;
                byte[] TimeBytes;
                byte[] dateTimeBytes = new byte[8];
                long Start;
                long End;

                // Initialize send buffer.  This will vary as the send loop runs.
                sendBuffer = new byte[packetSize - HeaderLen];
                for (int i = 0; i < sendBuffer.Length; i++) { sendBuffer[i] = 0x69; }

                // Figure out packet sizes, number of packets, buffer sizes, and inter-packet delay.
                long LifetimeInTicks = FlooderAction.TicksPerSecond * Action.TimeQuantum / 1000;
                double PacketsToSendDbl = ((double)bandwidth / (double)packetSize) * ((double)Action.TimeQuantum / (double)1000);
                PacketsToSendDbl /= Action.Concurrency;
                PacketsToSend = (int)PacketsToSendDbl;
                
                // It is unlikely that PacketsToSendDbl will be an integer, but I cannot send half a packet to the host without
                // compromising the packet size for at least one packet.  This code adds an extra packet based on random chance.
                if (r.NextDouble() < PacketsToSendDbl - PacketsToSend) { PacketsToSend++; }

                if (PacketsToSend > 0)
                {
                    waitInterval = LifetimeInTicks / PacketsToSend;
                }

                result = new FlooderResult();

                //Debug.WriteLine("Sending " + PacketsToSend);
                //Debug.WriteLine("Delay " + waitInterval);

                ulong MaxSeqNumber = SeqNum + (ulong)PacketsToSend;
                ulong MinSeqNumber = SeqLastSeen == null ? 0 : (ulong)SeqLastSeen + 1;

                // Kick off receive thread.  Lifetime is expressed in timer ticks.
                // new Task((resultObj) => { return Receive(long LifetimeInTicks); });
                Task<FlooderResult> Receiver = receiverFactory.StartNew(() => { return Receive(SW.ElapsedTicks + LifetimeInTicks, MinSeqNumber, MaxSeqNumber); });

                if (PacketsToSend == 0)
                {
                    Thread.Sleep(Action.TimeQuantum);
                }
                else
                {
                    for (int i = 0; i < PacketsToSend; i++)
                    {
                        // Start measuring this send packet event.
                        SendStart = SW.ElapsedTicks;
                        
                        // Take the sequence number and current number of elapsed ticks, and add them to the top of the send buffer.
                        SeqBytes = BitConverter.GetBytes(SeqNum);
                        TimeBytes = BitConverter.GetBytes(SW.ElapsedTicks);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(SeqBytes);
                            Array.Reverse(TimeBytes);
                        }
                        Array.ConstrainedCopy(SeqBytes, 0, sendBuffer, 0, 8);
                        Array.ConstrainedCopy(TimeBytes, 0, sendBuffer, 8, 8);

                        // Fire.
                        try
                        {
                            UDPSocket.Send(sendBuffer);
                        }
                        catch (SocketException)
                        { }
                        finally
                        {
                            SeqNum++;
                            result.PacketsSent++;
                        }

                        // The wai-ai-ting is the harr-dest paaaart.  Sender() tries to be nice to other threads.  However it is 
                        // unpredictable how much time Thread.Yield() will actually yield to other running threads.  The largest 
                        // value I observed on my hardware was 2700 ticks.  Your mileage may vary.  Wildly.  This code measures
                        // the time between Yield() calls and tries to make a best guess when to start looping without yielding 
                        // time to other threads (ensuring the next packet gets off on time).
                        while (SW.ElapsedTicks < waitInterval + SendStart - LargestSlice)
                        {
                            Start = SW.ElapsedTicks;
                            Thread.Yield();
                            End = SW.ElapsedTicks;
                            if (LargestSlice < End - Start) { LargestSlice = End - Start; }
                        }
                        while (SW.ElapsedTicks < waitInterval + SendStart) { }
                        // Done.  Repeat until all packets to send are sent.
                    }
                }
                // Arriving at the end of TimeQuantum.
                // Wrap up the sender statistics.
                result.OctetsSent = result.PacketsSent * packetSize;
                result.EndTime = DateTime.Now;
                // Get results from receiver thread. Merge results with send thread.  Send results back to UI thread.
                // This concludes the receiver thread.  A new one will be started in the next TimeQuantum.
                FlooderResult receiverResult = Receiver.Result;
                result += receiverResult;
                if (result.SeqLastSeen.HasValue)
                {
                    SeqLastSeen = result.SeqLastSeen;
                }
                // Send result to the main UI thread.
                worker.ReportProgress(0, result);
                result = null;
                //Debug.WriteLine("largest slice: " + LargestSlice);
            }
            //********** END Main Loop

            // Cancel was received.  Shutting down.
            IsRunning = false;
            //SW.Stop();
            if (UDPSocket != null)
            {
                UDPSocket.Shutdown(SocketShutdown.Both);
                UDPSocket.Close(0);
                UDPSocket = null;
            }
        }

        /// <summary>
        /// Receives UDP traffic, parses the payload, and records the arrival details.
        /// </summary>
        /// <param name="StopTime">Run until Stopwatch SW registers this number of ticks elapsed.</param>
        /// <param name="MinSeqNumber">The minimum sequence number to expect. Computed by sender thread.</param>
        /// <param name="MaxSeqNumber">The maximum sequence number to expect.  Computed by sender thread.</param>
        /// <returns>Results from receive operations</returns>
        private FlooderResult Receive(long StopTime, ulong MinSeqNumber, ulong MaxSeqNumber)
        {
            int recvBytes = 0;
            long timeReceived = 0;
            byte[] seqReceivedBytes = new byte[8]; 
            byte[] timeReceivedBytes = new byte[8];
            FlooderResult result = new FlooderResult();

            if (MaxSeqNumber - MinSeqNumber > Math.Pow(2,24))
            {
                MinSeqNumber = MaxSeqNumber - (ulong)Math.Pow(2,24);
            }

            int SequenceBuffer = (int)(MaxSeqNumber - MinSeqNumber);

            bool[] SequenceReceived = new bool[SequenceBuffer];

            while (SW.ElapsedTicks < StopTime)
            {
                while (UDPSocket.Available > 0)
                {
                    try
                    {
                        recvBytes = UDPSocket.Receive(recvBuffer, recvBuffer.Length, SocketFlags.None);
                    }
                    catch (SocketException)
                    { }
                    finally
                    {
                        timeReceived = SW.ElapsedTicks;
                        result.PacketsReceived++;
                        result.OctetsReceived += recvBytes + HeaderLen;
                        
                        Array.ConstrainedCopy(recvBuffer, 0, seqReceivedBytes, 0, 8);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(seqReceivedBytes);
                        }
                        ulong Sequence = BitConverter.ToUInt64(seqReceivedBytes, 0);
                        if (MinSeqNumber <= Sequence && Sequence <= MaxSeqNumber)
                        {
                            result.SeqLastSeen = Sequence;
                            SequenceReceived[Sequence - MinSeqNumber] = true;
                        }
                        Array.ConstrainedCopy(recvBuffer, 8, timeReceivedBytes, 0, 8);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(timeReceivedBytes);
                        }
                        long originTime = BitConverter.ToInt64(timeReceivedBytes, 0);
                        result.SumOfLatency += timeReceived - originTime;
                    }
                }
                Thread.Yield();
            }
            if(result.SeqLastSeen != null)
            {
                result.PacketsLost = 0;
                for (int i = 0; i < (int)((ulong)result.SeqLastSeen - MinSeqNumber); i++)
                {
                    if (SequenceReceived[i] == false)
                    {
                        result.PacketsLost++;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Called internally when a FlooderThread property changes
        /// </summary>
        /// <param name="PropertyName">String containing the property's name</param>
        protected void OnPropertyChanged(string PropertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(PropertyName));
            }
        }

    }
}
