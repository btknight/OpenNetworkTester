using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using NetworkFlooder;
using System.Windows.Input;

namespace ONT

{
    /// <summary>
    /// The main window's view model.  Contains properties for all window elements that are dynamic.
    /// </summary>
    public class MainWindowVM : INotifyPropertyChanged
    {
        /// <summary>
        /// Notifies other objects that a property has changed on this object.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Testing target, expressed as a hostname, or IPv4 address, or IPv6 address.
        /// </summary>
        public string Target
        {
            get
            {
                return this._target;
            }
            set
            {
                this._target = value;
                Debug.WriteLine("Target = " + value);
                OnPropertyChanged("Target");
                TargetIP = null;
                TargetResolveResult = "";
                if (value != "")
                {
                    Target_GetHostEntry();
                }
            }
        }
        private string _target;

        /// <summary>
        /// Testing target IP, after GetHostEntry() is called on Target.
        /// </summary>
        public IPAddress TargetIP
        {
            get { return _targetIP; }
            set
            {
                _targetIP = value;
                OnPropertyChanged("TargetIP");
                if (_flooder != null)
                {
                    _flooder.Action.TargetIP = value;
                }
                TargetPingResult = "";
                Target_Ping();
            }
        }
        private IPAddress _targetIP;

        /// <summary>
        /// Results of GetHostEntry(Target) in textual format.
        /// </summary>
        public string TargetResolveResult
        {
            get { return _targetResolveResult; }
            protected set
            {
                _targetResolveResult = value;
                OnPropertyChanged("TargetResolveResult");
            }
        }
        private string _targetResolveResult;

        /// <summary>
        /// Results of Target_Ping() on TargetIP.
        /// </summary>
        public string TargetPingResult
        {
            get { return _targetPingResult; }
            protected set
            {
                _targetPingResult = value;
                OnPropertyChanged("TargetPingResult");
            }
        }
        private string _targetPingResult;

        /// <summary>
        /// Maximum MTU available to the system.
        /// </summary>
        public int MaxMtu
        {
            get
            {
                return _maxMtu;
            }
            set
            {
                _maxMtu = value;
                OnPropertyChanged("MaxMtu");
            }
        }
        private int _maxMtu;

        /// <summary>
        /// Sending bandwidth, in octets/sec
        /// </summary>
        public int BandwidthOctets
        {
            get { return _bandwidth; }
            set
            {
                this._bandwidth = value;
                BandwidthChanged();
            }
        }

        /// <summary>
        /// Sending bandwidth, stored in octets/sec
        /// </summary>
        private int _bandwidth;

        /// <summary>
        /// Collection describing tick marks on the slider bar for bandwidth.
        /// </summary>
        public DoubleCollection BandwidthTicks { get; set; }

        /// <summary>
        /// Packet size, in octets/pkt
        /// </summary>
        public int PacketSizeOctets
        {
            get { return _packetSize; }
            set
            {
                this._packetSize = value;
                Debug.WriteLine("BW = " + _packetSize.ToString());
                _flooder.Action.PacketSize = value;
                OnPropertyChanged("PacketSizeOctets");
                OnPropertyChanged("PacketSizeBits");
                OnPropertyChanged("WANFrameSize");
            }
        }

        /// <summary>
        /// Packet size, in octets/pkt
        /// </summary>
        private int _packetSize;

        /// <summary>
        /// Destination UDP port
        /// </summary>
        public int DestPort
        {
            get
            {
                return _destPort;
            }
            set
            {
                _destPort = value;
                OnPropertyChanged("DestPort");
                _flooder.Action.DestPort = value;
            }
        }
        private int _destPort;

        /// <summary>
        /// True if Flooder is sending / receiving traffic, false if it is idle.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return _isRunning;
            }
            set
            {
                _isRunning = value;
                OnPropertyChanged("IsRunning");
                OnPropertyChanged("RunString");
            }
        }
        private bool _isRunning;

        /// <summary>
        /// String expression of IsRunning, used for button text.
        /// </summary>
        public string RunString
        {
            get
            {
                return IsRunning ? "Stop" : "Run";
            }
        }

        /// <summary>
        /// Set to true if the TargetIP has been resolved, false otherwise.
        /// </summary>
        public bool ReadyToRun
        {
            get { return _readyToRun; }
            set
            {
                _readyToRun = value;
                OnPropertyChanged("ReadyToRun");
            }
        }
        private bool _readyToRun;

        /// <summary>
        /// Enumeration expressing IsRunning, used for flashy blinky red text.
        /// </summary>
        public Visibility FlooderIsRunning
        {
            get { return _flooderIsRunning; }
            private set
            {
                _flooderIsRunning = value;
                OnPropertyChanged("FlooderIsRunning");
            }
        }
        private Visibility _flooderIsRunning;

        /// <summary>
        /// Function that, given an IP payload size, computes the layer 2 frame size.
        /// </summary>
        public WANFrameType SelectedWANFrameType
        {
            get
            {
                return _selectedWANFrameType;
            }
            set
            {
                _selectedWANFrameType = value;
                OnPropertyChanged("SelectedWANFrameType");
                OnPropertyChanged("WANFrameSize");
            }
        }
        private WANFrameType _selectedWANFrameType;

        /// <summary>
        /// Textbox display showing size of the packet with layer 2 header and trailer.
        /// </summary>
        public int WANFrameSize
        {
            get
            {
                return SelectedWANFrameType.Size + PacketSizeOctets;
            }
        }

        /// <summary>
        /// Boolean indicating whether to include the WAN frame size in the bandwidth usage calculations.
        /// </summary>
        public bool UseWANFrameBwCalc
        {
            get
            {
                return _useWANFrameBwCalc;
            }
            set
            {
                _useWANFrameBwCalc = value;
                OnPropertyChanged("UseWANFrameBwCalc");
            }
        }
        private bool _useWANFrameBwCalc;

        /// <summary>
        /// Starts and stops testing.
        /// </summary>
        public ICommand GoCommand
        {
            get
            {
                return _goCommand;
            }
            private set
            {
                _goCommand = value;
            }
        }
        private ICommand _goCommand;

        /// <summary>
        /// Collection describing tick marks on the slider bar for packet size.
        /// </summary>
        public DoubleCollection PacketSizeTicks { get; set; }

        public FlooderResultVM LatestResult
        {
            get
            {
                return _latestResult;
            }
            private set
            {
                _latestResult = value;
                OnPropertyChanged("LatestResult");
            }
        }
        private FlooderResultVM _latestResult;

        public FlooderResultVM HistoryResult
        {
            get
            {
                return _historyResult;
            }
            private set
            {
                _historyResult = value;
                OnPropertyChanged("HistoryResult");
            }
        }
        private FlooderResultVM _historyResult;


        /// <summary>
        /// Flooder object.  Controls network send and receive operations.
        /// </summary>
        protected Flooder _flooder;

        public MainWindowVM()
        {
            _flooder = new Flooder();
            _flooder.PropertyChanged += flooder_PropertyChanged;
            FlooderIsRunning = Visibility.Hidden;
            LatestResult = null;
            HistoryResult = null;
            Target = "<enter hostname here>";
            PacketSizeOctets = 1250;
            BandwidthOctets = 125000;
            DestPort = 7;
            TargetIP = null;
            ReadyToRun = false;
            TargetResolveResult = "";
            TargetPingResult = "";
            GoCommand = new GoCommand(() => {
                if (IsRunning) { _flooder.Stop(); } else { _flooder.Start(); }
                IsRunning = !IsRunning;
            }, true);
            SelectedWANFrameType = new WANFrameType("Ethernet", 26);
            UseWANFrameBwCalc = false;

            FindMaxMTU();

            DoubleCollection BandwidthTicksBits = new DoubleCollection();
            BandwidthTicksBits.Add(1000000);
            BandwidthTicksBits.Add(1536000);
            BandwidthTicksBits.Add(1536000 * 2);
            BandwidthTicksBits.Add(5000000);
            BandwidthTicksBits.Add(1536000 * 3);
            BandwidthTicksBits.Add(1536000 * 4);
            BandwidthTicksBits.Add(10000000);
            BandwidthTicksBits.Add(20000000);
            BandwidthTicksBits.Add(50000000);
            BandwidthTicksBits.Add(100000000);
            BandwidthTicksBits.Add(200000000);
            BandwidthTicksBits.Add(500000000);
            BandwidthTicksBits.Add(1000000000);

            /* The below math formula causes the following to happen on the slider control:
             *   From 0 to 1 megabit/sec (1000000 bits/sec), the scale is linear.
             *   From 1 megabit/sec to 1 gigabit/sec, the scale is logarithmic.
             * The formula first converts the above numbers from bits/sec to octets/sec.
             * Then it takes the Log base 10 of that octets/sec number.
             * Finally, it aligns the start of the log scale with the linear scale, by subtracting Log base 10 of 1 megabit/sec (125,000 octets/sec), minus 1.
             */

            BandwidthTicks = new DoubleCollection(BandwidthTicksBits.Select(x => { return Math.Log10(x / 8) - (Math.Log10(125000) - 1); }));

            PacketSizeTicks = new DoubleCollection();
            PacketSizeTicks.Add(64);
            PacketSizeTicks.Add(576);
            PacketSizeTicks.Add(1250);
            PacketSizeTicks.Add(1500);
            PacketSizeTicks.Add(9000);
        }

        /// <summary>
        /// Raised when the Flooder object updates one of its own properties.
        /// </summary>
        /// <param name="sender">Flooder object sending the message</param>
        /// <param name="e">Object containing name of the property that changed</param>
        void flooder_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Flooder f = (Flooder)sender;
            int L2Octets = 0;
            if (UseWANFrameBwCalc)
            {
                L2Octets = SelectedWANFrameType.Size;
            }
            if (e.PropertyName == "IsRunning")
            {
                if (f.IsRunning)
                    FlooderIsRunning = Visibility.Visible;
                else
                    FlooderIsRunning = Visibility.Hidden;
            }
            if (e.PropertyName == "LatestResult")
            {
                LatestResult = new FlooderResultVM(f.LatestResult, L2Octets);
                //ComputeImmediateResult(f.LatestResult);
            }
            if (e.PropertyName == "HistoryResult")
            {
                HistoryResult = new FlooderResultVM(f.HistoryResult, L2Octets);
                //ComputeHistoricalResult(f.HistoryResult);
            }
        }

        /// <summary>
        /// Finds the maximum MTU of all network cards in the system.
        /// </summary>
        protected void FindMaxMTU()
        {
            // TODO: Find a better way to handle different MTU's on different interfaces
            int maxMtu = 0;
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var intf in interfaces)
            {
                int intfMtu = 0;
                try
                {
                    intfMtu = intf.GetIPProperties().GetIPv6Properties().Mtu;
                }
                catch (NetworkInformationException)
                {
                    intfMtu = 0;
                }
                if (maxMtu < intfMtu)
                {
                    maxMtu = intfMtu;
                }
                try
                {
                    intfMtu = intf.GetIPProperties().GetIPv4Properties().Mtu;
                }
                catch (NetworkInformationException)
                {
                    intfMtu = 0;
                }
                if (maxMtu < intfMtu)
                {
                    maxMtu = intfMtu;
                }
            }
            MaxMtu = maxMtu;
        }

        /// <summary>
        /// Called internally when a property changes value.
        /// </summary>
        /// <param name="PropertyName">Name of the changed property</param>
        protected void OnPropertyChanged(string PropertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(PropertyName));
            }
        }

        /// <summary>
        /// Given a hostname or IP, finds an IP address.
        /// </summary>
        async protected void Target_GetHostEntry()
        {
            IPAddress targetIP;
            if (!IPAddress.TryParse(Target, out targetIP))
            {
                Debug.WriteLine("Resolving IP");
                IPHostEntry entry;
                try
                {
                    Task<IPHostEntry> result = Dns.GetHostEntryAsync(Target);
                    entry = await result;
                }
                catch (SocketException e)
                {
                    TargetResolveResult = e.Message;
                    TargetIP = null;
                    ReadyToRun = false;
                    return;
                }
                targetIP = entry.AddressList[0];
            }
            TargetResolveResult = "Target IP Address: " + targetIP.ToString();
            TargetIP = targetIP;
            ReadyToRun = true;
        }

        /// <summary>
        /// Called when TargetIP is changed.  Attempts to ping the IP.
        /// </summary>
        async protected void Target_Ping()
        {
            if (TargetIP != null)
            {
                Ping sender = new Ping();
                for (int i = 0; i < 5; i++)
                {
                    Debug.WriteLine("Pinging " + (i+1) + " / 5");
                    Task<PingReply> task = sender.SendPingAsync(TargetIP, 250);
                    PingReply result = await task;
                    if (result.Status == IPStatus.Success)
                    {
                        TargetPingResult = "Target is pingable";
                        return;
                    }
                }
                TargetPingResult = "No ping response from target";
                return;
            }
            TargetPingResult = "";
        }

        /// <summary>
        /// Called whenever send bandwidth is changed.
        /// </summary>
        protected void BandwidthChanged()
        {
            if (BandwidthOctets == 0 && IsRunning)
            {
                _flooder.Stop();
                IsRunning = false;
                MessageBox.Show("Bandwidth adjusted to zero - halting test");
            }
            _flooder.Action.BandwidthOctets = BandwidthOctets;
            Debug.WriteLine("BW = " + _bandwidth.ToString());
            OnPropertyChanged("BandwidthOctets");
        }
    }
}
