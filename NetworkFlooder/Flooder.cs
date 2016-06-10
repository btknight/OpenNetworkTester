using System.Timers;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;

namespace NetworkFlooder
{
    /// <summary>
    /// Manages FlooderThread objects.  Starts and stops the worker threads as needed, 
    /// and funnels objects from each back to the UI.
    /// </summary>
    public class Flooder : INotifyPropertyChanged
    {
        /// <summary>
        /// FlooderAction object that controls the behavior of sending threads.  Polled by threads once per FlooderAction.TimeQuantum.
        /// </summary>
        public FlooderAction Action
        {
            get { return _action; }
            private set
            {
                _action = value;
                OnPropertyChanged("Action");
            }
        }
        private FlooderAction _action;

        /// <summary>
        /// True if threads are sending and receiving, false if idle.
        /// </summary>
        public bool IsRunning
        {
            get { return _isRunning; }
            private set
            {
                _isRunning = value;
                OnPropertyChanged("IsRunning");
            }
        }
        private bool _isRunning;

        /// <summary>
        /// Internal array of FlooderThreads.
        /// </summary>
        private FlooderThread[] ft;

        /// <summary>
        /// Notifies other objects that a property has changed on this object.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Latest result from the flooder threads.
        /// </summary>
        public FlooderResult LatestResult
        {
            get { return _latestResult; }
            private set
            {
                _latestResult = value;
                OnPropertyChanged("LatestResult");
            }
        }
        private FlooderResult _latestResult;

        /// <summary>
        /// Internal ring buffer that stores five seconds of FlooderResults.
        /// </summary>
        private FlooderResult[] History;

        /// <summary>
        /// History ring buffer pointer.
        /// </summary>
        private int HistoryCounter;

        /// <summary>
        /// Combined result from all objects in the history ring buffer.
        /// </summary>
        public FlooderResult HistoryResult
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
        private FlooderResult _historyResult;
        
        /// <summary>
        /// Used to monitor timing of updates from the sender threads.
        /// </summary>
        private System.Timers.Timer UIUpdateTimer;
        
        /// <summary>
        /// Monitors whether updates have been received from all threads.
        /// </summary>
        private bool[] UpdateReceived;

        public Flooder()
        {
            Action = new FlooderAction();
            IsRunning = false;
            ft = new FlooderThread[Action.Concurrency];
            UpdateReceived = new bool[Action.Concurrency];
            LatestResult = new FlooderResult();
            History = new FlooderResult[5000 / Action.TimeQuantum];
            HistoryCounter = 0;
            UIUpdateTimer = new System.Timers.Timer(Action.TimeQuantum);
            UIUpdateTimer.Elapsed += UIUpdateTimer_Elapsed;
            for (int i = 0; i < Action.Concurrency; i++)
            {
                ft[i] = new FlooderThread(Action);
                ft[i].PropertyChanged += FlooderThread_PropertyChanged;
            }
        }

        /// <summary>
        /// Called when the UIUpdateTimer expires.  Used to ensure constant updates are flowing to the UI.
        /// </summary>
        /// <param name="sender">UIUpdateTimer object (ignored)</param>
        /// <param name="e">ElapsedEventArgs (ignored)</param>
        void UIUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ProcessUpdates();
        }

        /// <summary>
        /// Pulls updates from the FlooderThreads and builds combined FlooderResult objects for LatestUpdate and HistoryResult.
        /// </summary>
        void ProcessUpdates()
        {
            // Sum latest results from all threads.
            FlooderResult resultSum = new FlooderResult();
            for (int i = 0; i < Action.Concurrency; i++)
            {
                resultSum += ft[i].LatestResult;
                UpdateReceived[i] = false;
            }
            LatestResult = resultSum;
            
            // Add to History[] array, and recompute single HistoryResult.
            History[HistoryCounter] = resultSum;
            HistoryCounter = (HistoryCounter + 1) % History.Length;
            FlooderResult historyResult = new FlooderResult();
            for (int i = 0; i < History.Length; i++)
            {
                if (History[i] != null)
                {
                    historyResult += History[i];
                }
            }
            HistoryResult = historyResult;
        }

        /// <summary>
        /// Called when a property in a FlooderThread object changes.
        /// </summary>
        /// <param name="sender">FlooderThread object sending the message</param>
        /// <param name="e">Contains name of changed property</param>
        void FlooderThread_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (IsRunning)  // if not running, ignore the update from the thread, it hasn't gotten the word that we're shut down
            {
                if (e.PropertyName == "LatestResult")
                {
                    // Start the timer if it is not yet running
                    if (!UIUpdateTimer.Enabled)
                    {
                        UIUpdateTimer.Start();
                    }
                    // Check if all updates have been received.
                    bool AllUpdatesReceived = true;                 // will be ANDed with the update status of other threads
                    for (int i = 0; i < Action.Concurrency; i++)
                    {
                        if (ft[i] == sender)                        // if the sender is the one with this index num, mark it in the UpdateReceived array
                        {
                            UpdateReceived[i] = true;
                        }
                        AllUpdatesReceived &= UpdateReceived[i];    // Logical AND all of the UpdateReceived[] array - if any are false, AllUpdatesReceived == false
                    }
                    // If all updates have been received, stop the UI update timer, and process all thread updates.
                    if (AllUpdatesReceived)
                    {
                        UIUpdateTimer.Stop();
                        ProcessUpdates();
                    }
                }
            }
        }

        /// <summary>
        /// Starts all threads.
        /// </summary>
        public void Start()
        {
            foreach (FlooderThread t in ft)
            {
                t.Start();
            }
            IsRunning = true;
        }

        /// <summary>
        /// Stops all threads, and clears all Results objects.
        /// </summary>
        public void Stop()
        {
            foreach (FlooderThread t in ft)
            {
                t.Stop();
            }
            UIUpdateTimer.Stop();
            IsRunning = false;
            LatestResult = new FlooderResult();
            HistoryResult = new FlooderResult();
            for (int i = 0; i < History.Length; i++)
            {
                History[i] = null;
            }
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
    }
}
