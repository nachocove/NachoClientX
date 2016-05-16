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
using System.Linq;

namespace NachoCore.Brain
{
    public partial class NcBrain
    {
        public const bool ENABLED = true;

        public static bool RegisterStatusIndHandler = false;

        public static int StartupDelayMsec = 500;

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

        private object ProcessLoopLockObj;
        private object SyncRoot;

        private NcBrainNotification NotificationRateLimiter;

        public NcBrain (string prefix = "Brain")
        {
            _Indexes = new ConcurrentDictionary<int, NcIndex> ();

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
            NcTask.Run (() => {
                NcBrain brain = NcBrain.SharedInstance;
                lock (brain.SyncRoot) {
                    if (brain.IsRunning) {
                        Log.Info (Log.LOG_BRAIN, "NcBrain.StartService() called when the brain is already running.");
                        return;
                    }
                    var token = NcTask.Cts.Token;
                    brain.EventQueue.Token = token;
                    brain.IsRunning = true;
                    // Remove any TERMINATE events at the front of the queue.  They were intended for
                    // the previous run of the brain, not this session.
                    while (null != brain.EventQueue.DequeueIf (evt => { return evt.Type == NcBrainEventType.TERMINATE; })) {
                    }
                }
                try {
                    brain.Process ();
                } finally {
                    lock (brain.SyncRoot) {
                        brain.IsRunning = false;
                    }
                }
            }, "Brain");
        }

        public static void StopService ()
        {
            var brain = NcBrain.SharedInstance;
            lock (brain.SyncRoot) {
                if (brain.IsRunning) {
                    brain.IsRunning = false;
                    brain.EventQueue.Undequeue (new NcBrainEvent (NcBrainEventType.TERMINATE));
                }
            }
        }

        private bool IsInUnitTest ()
        {
            return (0 == StartupDelayMsec);
        }

        private bool RunSeveralTimes (string name, BrainQueryAndProcess action, int maxCount)
        {
            int numberProcessed = 0;
            bool dummyResult;
            while (numberProcessed < maxCount && KeepGoing () && action.Process (out dummyResult)) {
                ++numberProcessed;
            }
            if (0 < numberProcessed) {
                Log.Info (Log.LOG_BRAIN, "{0}: {1} items processed", name, numberProcessed);
            }
            return 0 < numberProcessed;
        }

        private bool ProcessSeveralPersistedEvents (NcBrainEventType type, int maxCount)
        {
            int numberProcessed = 0;
            while (numberProcessed < maxCount && KeepGoing ()) {
                var brainEvent = McBrainEvent.QueryNextType (type);
                if (null == brainEvent) {
                    return 0 < numberProcessed;
                }
                ProcessEvent (brainEvent.BrainEvent ());
                brainEvent.Delete ();
                ++numberProcessed;
            }
            return 0 < numberProcessed;
        }

        public void Process ()
        {
            bool tvStarted = false;
            if (ENABLED && !IsInUnitTest ()) {
                // Delay brain to avoid initialization logjam
                if (!NcTask.CancelableSleep (StartupDelayMsec)) {
                    NcTask.Cts.Token.ThrowIfCancellationRequested ();
                }

                // Only start the time variance stuff when in the foreground.
                if (NcApplication.Instance.IsForeground) {
                    McEmailMessage.StartTimeVariance (EventQueue.Token);
                    tvStarted = true;
                }
                if (!NcBrain.RegisterStatusIndHandler) {
                    NcApplication.Instance.StatusIndEvent += StatusIndicationHandler;
                    NcBrain.RegisterStatusIndHandler = true;
                }
            }
            lock (ProcessLoopLockObj) {

                bool didSomething = true;
                while (true) {

                    if (didSomething) {
                        OpenedIndexes.Cleanup ();
                    }

                    if (!tvStarted && !IsInUnitTest () && NcApplication.Instance.IsForeground) {
                        McEmailMessage.StartTimeVariance (EventQueue.Token);
                        tvStarted = true;
                    }

                    // Priority 1: Events in the queue.
                    // If there is nothing to do, then the Dequeue call will block until
                    // something is added to the queue.
                    if (!didSomething || !EventQueue.IsEmpty ()) {
                        var brainEvent = EventQueue.Dequeue ();
                        if (NcBrainEventType.TERMINATE == brainEvent.Type) {
                            Log.Info (Log.LOG_BRAIN, "NcBrain Task exits");
                            return;
                        }
                        if (ENABLED) {
                            ProcessEvent (brainEvent);
                        }
                        didSomething = true;
                        continue;
                    }

                    didSomething = false;

                    // Priority 2: Quick scoring of new messages
                    if (KeepGoing () && RunSeveralTimes ("Quick score messages", QuickScore, 50)) {
                        didSomething = true;
                    }

                    // Only P1 and P2 are done in quick sync mode.
                    if (didSomething || !KeepGoing () || !NcApplication.Instance.IsForegroundOrBackground) {
                        continue;
                    }

                    // Priority 3: Glean and score unscored messages. Update the score of messages with
                    // a high NeedsUpdate count. Index newly arrived messages.
                    if (KeepGoing () && RunSeveralTimes ("Glean/analyze messages", AnalyzeEmail, 30)) {
                        didSomething = true;
                    }
                    if (KeepGoing () && RunSeveralTimes ("Update message scores (high)", UpdateScoreHigh, 20)) {
                        didSomething = true;
                    }
                    if (KeepGoing () && ProcessSeveralPersistedEvents (NcBrainEventType.INDEX_MESSAGE, 10)) {
                        didSomething = true;
                    }
                    if (didSomething || !KeepGoing ()) {
                        continue;
                    }

                    // Priority 4: Update the score of messages with a low NeedsUpdate count. Unindex deleted
                    // messages. Reindex messages that have been downloaded.
                    if (KeepGoing () && RunSeveralTimes ("Update message scores (low)", UpdateScoreLow, 20)) {
                        didSomething = true;
                    }
                    if (KeepGoing () && ProcessSeveralPersistedEvents (NcBrainEventType.UNINDEX_MESSAGE, 10)) {
                        didSomething = true;
                    }
                    if (KeepGoing () && RunSeveralTimes ("Index messages", IndexEmail, 10)) {
                        didSomething = true;
                    }
                    if (didSomething || !KeepGoing () || !NcApplication.Instance.IsForeground) {
                        continue;
                    }

                    // Priority 5: Index/reindex/unindex contacts.  This is only done when in the foreground.
                    // Indexing of contacts is the lowest priority because the indexes are rarely used.
                    if (KeepGoing () && RunSeveralTimes ("Index contacts", IndexContacts, 20)) {
                        didSomething = true;
                    }
                    if (KeepGoing () && ProcessSeveralPersistedEvents (NcBrainEventType.REINDEX_CONTACT, 20)) {
                        didSomething = true;
                    }
                    if (KeepGoing () && ProcessSeveralPersistedEvents (NcBrainEventType.UNINDEX_CONTACT, 20)) {
                        didSomething = true;
                    }

                    // Finally, handle any persistent events that slipped through the cracks.  This could be
                    // persisted events that were left over from before the app was upgraded to the current
                    // Brain event scheme.  Though sometimes this will catch an INDEX_MESSAGE event that was
                    // added while querying for other kinds of work.
                    if (!didSomething && KeepGoing ()) {
                        var brainEvent = McBrainEvent.QueryNext ();
                        if (null != brainEvent) {
                            ProcessEvent (brainEvent.BrainEvent ());
                            brainEvent.Delete ();
                            didSomething = true;
                        }
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

        private static void StatusIndicationHandler (object sender, EventArgs args)
        {
            StatusIndEventArgs eventArgs = args as StatusIndEventArgs;
            switch (eventArgs.Status.SubKind) {

            case NcResult.SubKindEnum.Info_RicInitialSyncCompleted:
                NcBrain.SharedInstance.Enqueue (new NcBrainInitialRicEvent (eventArgs.Account.Id));
                break;

            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded:
                NcBrain.SharedInstance.Enqueue (new NcBrainEvent (NcBrainEventType.PERIODIC_GLEAN));
                break;
            }
        }

        public void ProcessOneNewEmail (McEmailMessage emailMessage)
        {
            NcContactGleaner.GleanContactsHeaderPart1 (emailMessage);

            var folder = McFolder.QueryByFolderEntryId<McEmailMessage> (emailMessage.AccountId, emailMessage.Id).FirstOrDefault ();
            if (null != folder && !folder.IsJunkFolder () && NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDeleted_4 != folder.Type) {
                NcBrain.IndexMessage (emailMessage);
            }
        }
    }
}
