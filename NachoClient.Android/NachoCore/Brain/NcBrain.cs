//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using MimeKit;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.Index;
using System.Threading;

namespace NachoCore.Brain
{
    public partial class NcBrain
    {
        public const bool ENABLED = true;

        public static bool RegisterStatusIndHandler = false;

        public static int StartupDelayMsec = 5000;

        private static volatile NcBrain _SharedInstance;
        private static object syncRoot = new Object ();

        public static NcBrain SharedInstance {
            get {
                if (null == _SharedInstance) {
                    lock (syncRoot) {
                        if (null == _SharedInstance) {
                            _SharedInstance = new NcBrain ();
                        }
                    }
                }
                return _SharedInstance;
            }
        }

        public class OperationCounters
        {
            private NcCounter Root;
            public NcCounter Insert;
            public NcCounter Delete;
            public NcCounter Update;

            public OperationCounters (string name, NcCounter Parent)
            {
                Root = Parent.AddChild (name);
                Insert = Root.AddChild ("Insert");
                Delete = Root.AddChild ("Delete");
                Update = Root.AddChild ("Update");
            }
        }

        private ConcurrentDictionary<int, NcIndex> _Indexes;

        public NcCounter RootCounter;
        public OperationCounters McEmailMessageCounters;
        public OperationCounters McEmailAddressCounters;
        public OperationCounters McEmailAddressScoreSyncInfo;

        private DateTime LastPeriodicGlean;
        private DateTime LastPeriodicGleanRestart;

        private object ProcessLoopLockObj;
        private object SyncRoot;

        private NcBrainNotification NotificationRateLimiter;

        public NcBrain (string prefix = "Brain")
        {
            _Indexes = new ConcurrentDictionary<int, NcIndex> ();

            LastPeriodicGlean = new DateTime ();
            LastPeriodicGleanRestart = new DateTime ();
            NotificationRateLimiter = new NcBrainNotification ();

            RootCounter = new NcCounter (prefix, true);
            McEmailMessageCounters = new OperationCounters ("McEmailMessage", RootCounter);
            McEmailAddressCounters = new OperationCounters ("McEmailAddress", RootCounter);
            RootCounter.AutoReset = true;
            RootCounter.ReportPeriod = 5 * 60; // report once every 5 min

            ProcessLoopLockObj = new object ();
            SyncRoot = new object ();

            InitializeEventHandler ();
        }

        public void Cleanup ()
        {
            RootCounter.Dispose ();
            RootCounter = null;
        }

        public NcIndex Index (int accountId)
        {
            NcIndex index;
            if (_Indexes.TryGetValue (accountId, out index)) {
                return index;
            }
            var indexPath = NcModel.Instance.GetIndexPath (accountId);
            index = new Index.NcIndex (indexPath);
            if (!_Indexes.TryAdd (accountId, index)) {
                // A race happens and this thread loses. There should be an Index in the dictionary now
                index.Dispose ();
                index = null;
                var got = _Indexes.TryGetValue (accountId, out index);
                NcAssert.True (got);
            }
            return index;
        }

        public bool IsRunning { get; protected set; }

        public static void StartService ()
        {
            // Set up the logging functions for IndexLib
            NcBrain brain = NcBrain.SharedInstance;
            NcTask.Run (() => {
                lock (brain.SyncRoot) {
                    var token = NcTask.Cts.Token;
                    token.Register (NcContactGleaner.Stop);
                    brain.EventQueue.Token = token;
                    NcContactGleaner.Start ();
                    brain.IsRunning = true;
                }
                brain.Process ();
            }, "Brain");
        }

        public static void StopService ()
        {
            lock (NcBrain.SharedInstance.SyncRoot) {
                NcBrain.SharedInstance.IsRunning = false;
                NcContactGleaner.Stop ();
                NcBrain.SharedInstance.EventQueue.Undequeue (new NcBrainEvent (NcBrainEventType.TERMINATE));
            }
        }

        public void PauseService ()
        {
            lock (SyncRoot) {
                if (IsRunning) {
                    NcContactGleaner.Stop ();
                    var pause = new NcBrainEvent (NcBrainEventType.PAUSE);
                    try {
                        EventQueue.UndequeueIfNot (pause, (obj) => {
                            NcBrainEvent evt = obj;
                            return evt.Type == NcBrainEventType.PAUSE;
                        });
                    } catch (OperationCanceledException) {
                        // This means brain is stopped, so ignore this
                    }
                }
            }
        }

        public void UnPauseService ()
        {
            lock (SyncRoot) {
                if (IsRunning) {
                    try {
                        EventQueue.DequeueIf ((obj) => {
                            NcBrainEvent evt = obj;
                            return evt.Type == NcBrainEventType.PAUSE;
                        });
                    } catch (OperationCanceledException) {
                        // This means brain is stopped, so ignore this
                    }
                    NcContactGleaner.Start ();
                }
            }
        }

        private bool IsInUnitTest ()
        {
            return (0 == StartupDelayMsec);
        }

        public void Process ()
        {
            bool tvStarted = false;
            if (ENABLED && !IsInUnitTest ()) {
                // Delay brain to avoid initialization logjam
                if (!NcTask.CancelableSleep (StartupDelayMsec)) {
                    NcTask.Cts.Token.ThrowIfCancellationRequested ();
                }

                // If brain task is running under quick sync, do not start time variance
                // as it is a waste of time.
                if (NcApplication.Instance.IsForegroundOrBackground) {
                    McEmailMessage.StartTimeVariance (EventQueue.Token);
                    tvStarted = true;
                }
                if (!NcBrain.RegisterStatusIndHandler) {
                    NcApplication.Instance.StatusIndEvent += StatusIndicationHandler;
                    NcBrain.RegisterStatusIndHandler = true;
                }
            }
            lock (ProcessLoopLockObj) {
                while (true) {
                    var brainEvent = EventQueue.Dequeue ();
                    if (NcBrainEventType.TERMINATE == brainEvent.Type) {
                        Log.Info (Log.LOG_BRAIN, "NcBrain Task exits");
                        return;
                    }
                    if (!IsInUnitTest ()) {
                        if (!tvStarted && NcApplication.Instance.IsForegroundOrBackground) {
                            McEmailMessage.StartTimeVariance (EventQueue.Token);
                            tvStarted = true;
                        }
                        // TODO - scheduling of brain actions need to be smarter. This will be
                        // addressed in brain 2.0.
                        if (!NcApplication.Instance.IsForegroundOrBackground) {
                            continue;
                        }
                    }
                    if (ENABLED) {
                        ProcessEvent (brainEvent);
                    }
                }
            }
        }

        public void NotifyEmailAddressUpdates ()
        {
            NotificationRateLimiter.NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
        }

        public void NotifyEmailMessageUpdates ()
        {
            NotificationRateLimiter.NotifyUpdates (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated);
        }

        public void StatusIndicationHandler (object sender, EventArgs args)
        {
            StatusIndEventArgs eventArgs = args as StatusIndEventArgs;
            switch (eventArgs.Status.SubKind) {
            case NcResult.SubKindEnum.Info_RicInitialSyncCompleted:
                // Status indication handler for Info_RicInitialSyncCompleted. We do not 
                // generate initial email address scores from RIC in this function for 2
                // reasons. First, we do not want to hold up the status indication callback
                // for a long duration. Second, the callback may be in a different threads
                // as NcBrain task. So, we may have two threads updating the same object.
                // Therefore, we enqueue a brain event and let brain task to do the actual
                // processing.
                var initialRicEvent = new NcBrainInitialRicEvent (eventArgs.Account.Id);
                NcBrain.SharedInstance.Enqueue (initialRicEvent);
                break;
            case NcResult.SubKindEnum.Info_BackgroundAbateStopped:
                DateTime now = DateTime.Now;
                if (((double)NcContactGleaner.GLEAN_PERIOD < (now - LastPeriodicGlean).TotalSeconds) &&
                    (LastPeriodicGleanRestart < LastPeriodicGlean)) {
                    LastPeriodicGleanRestart = now;
                    NcContactGleaner.Stop ();
                    NcContactGleaner.Start ();
                }
                break;
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
                var stateMachineEvent = new NcBrainStateMachineEvent (eventArgs.Account.Id, 100);
                NcBrain.SharedInstance.Enqueue (stateMachineEvent);
                break;
            }
        }

        public void ProcessOneNewEmail (McEmailMessage emailMessage)
        {
            NcContactGleaner.GleanContactsHeaderPart1 (emailMessage);
        }
    }
}
