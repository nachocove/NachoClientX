//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
//#define INDEXING_ENABLED

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using MimeKit;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCoreLog = NachoCore.Index.Log;

namespace NachoCore.Brain
{
    public partial class NcBrain
    {
        public const bool ENABLED = true;

        public static bool RegisterStatusIndHandler = false;

        private static NcBrain _SharedInstance;

        public static NcBrain SharedInstance {
            get {
                if (null == _SharedInstance) {
                    _SharedInstance = new NcBrain ();
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

        private NcQueue<NcBrainEvent> EventQueue;

        public NcCounter RootCounter;
        public OperationCounters McEmailMessageCounters;
        public OperationCounters McEmailMessageDependencyCounters;
        public OperationCounters McEmailMessageScoreSyncInfoCounters;
        public OperationCounters McEmailAddressCounters;
        public OperationCounters McEmailAddressScoreSyncInfo;

        private DateTime LastPeriodicGlean;
        private DateTime LastPeriodicGleanRestart;

        private ConcurrentDictionary<NcResult.SubKindEnum, DateTime> LastNotified;

        private object LockObj;

        public NcBrain ()
        {
            LastPeriodicGlean = new DateTime ();
            LastPeriodicGleanRestart = new DateTime ();
            LastNotified = new ConcurrentDictionary<NcResult.SubKindEnum, DateTime> ();

            RootCounter = new NcCounter ("Brain", true);
            McEmailMessageCounters = new OperationCounters ("McEmailMessage", RootCounter);
            McEmailMessageDependencyCounters = new OperationCounters ("McEmailMessageDependency", RootCounter);
            McEmailMessageScoreSyncInfoCounters = new OperationCounters ("McEmailMessageScoreSyncInfo", RootCounter);
            McEmailAddressCounters = new OperationCounters ("McEmailAddress", RootCounter);
            McEmailAddressScoreSyncInfo = new OperationCounters ("McEmailAddressScoreSyncInfo", RootCounter);
            RootCounter.AutoReset = true;
            RootCounter.ReportPeriod = 5 * 60; // report once every 5 min

            EventQueue = new NcQueue<NcBrainEvent> ();
            LockObj = new object ();
        }

        public static void StartService ()
        {
            // Set up the logging functions for IndexLib
            NachoCoreLog.PlatformDebug = (fmt, args) => {
                Log.Info (Log.LOG_BRAIN, fmt, args);
            };
            NachoCoreLog.PlatformInfo = (fmt, args) => {
                Log.Info (Log.LOG_BRAIN, fmt, args);
            };
            NachoCoreLog.PlatformWarn = (fmt, args) => {
                Log.Warn (Log.LOG_BRAIN, fmt, args);
            };
            NachoCoreLog.PlatformError = (fmt, args) => {
                Log.Error (Log.LOG_BRAIN, fmt, args);
            };

            NcBrain brain = NcBrain.SharedInstance;
            NcTask.Run (() => {
                brain.EventQueue.Token = NcTask.Cts.Token;
                brain.Process ();
            }, "Brain");
        }

        public static void StopService ()
        {
            NcBrain.SharedInstance.SignalTermination ();
        }

        public void Enqueue (NcBrainEvent brainEvent)
        {
            EventQueue.Enqueue (brainEvent);
        }

        public void SignalTermination ()
        {
            var brainEvent = new NcBrainEvent (NcBrainEventType.TERMINATE);
            EventQueue.Undequeue (brainEvent);
        }

        public bool IsQueueEmpty ()
        {
            return EventQueue.IsEmpty ();
        }

        private int GleanContacts (int count, Int64 accountId = -1)
        {
            int numGleaned = 0;
            bool quickGlean = false;
            string accountAddress = null;
            if (0 < accountId) {
                var account = McAccount.QueryById<McAccount> ((int)accountId);
                if ((null != account) && (!String.IsNullOrEmpty (account.EmailAddr))) {
                    accountAddress = account.EmailAddr;
                    quickGlean = true;
                }
            }
            while (numGleaned < count && !NcApplication.Instance.IsBackgroundAbateRequired &&
                   !EventQueue.Token.IsCancellationRequested) {
                McEmailMessage emailMessage = McEmailMessage.QueryNeedGleaning (accountId);
                if (null == emailMessage) {
                    break;
                }
                Log.Info (Log.LOG_BRAIN, "glean contact from email message {0}", emailMessage.Id);
                NcContactGleaner.GleanContacts (emailMessage.AccountId, emailMessage, quickGlean);
                if (quickGlean) {
                    // Assign a version 0 score by checking if our address is in the to list
                    InternetAddressList addressList = NcEmailAddress.ParseAddressListString (emailMessage.To);
                    foreach (var address in addressList) {
                        if (!(address is MailboxAddress)) {
                            continue;
                        }
                        if (((MailboxAddress)address).Address == accountAddress) {
                            NcAssert.True (0 == emailMessage.ScoreVersion); // shouldn't be gleaning if version > 0
                            emailMessage.Score = McEmailMessage.minHotScore;
                            emailMessage.UpdateByBrain ();
                            break;
                        }
                    }
                }
                numGleaned++;
            }
            if (0 != numGleaned) {
                Log.Info (Log.LOG_BRAIN, "{0} email message gleaned", numGleaned);
                NotifyUpdates (NcResult.SubKindEnum.Info_ContactSetChanged);
            }
            return numGleaned;
        }

        private int AnalyzeEmailAddresses (int count)
        {
            int numAnalyzed = 0;
            while (numAnalyzed < count && !NcApplication.Instance.IsBackgroundAbateRequired &&
                   !EventQueue.Token.IsCancellationRequested) {
                McEmailAddress emailAddress = McEmailAddress.QueryNeedAnalysis ();
                if (null == emailAddress) {
                    break;
                }
                Log.Info (Log.LOG_BRAIN, "analyze email address {0}", emailAddress.Id);
                emailAddress.ScoreObject ();
                numAnalyzed++;
            }
            if (0 != numAnalyzed) {
                Log.Info (Log.LOG_BRAIN, "{0} email addresses analyzed", numAnalyzed);
            }
            return numAnalyzed;
        }

        private int AnalyzeEmails (int count)
        {
            int numAnalyzed = 0;
            while (numAnalyzed < count && !NcApplication.Instance.IsBackgroundAbateRequired &&
                   !EventQueue.Token.IsCancellationRequested) {
                McEmailMessage emailMessage = McEmailMessage.QueryNeedAnalysis ();
                if (null == emailMessage) {
                    break;
                }
                Log.Debug (Log.LOG_BRAIN, "analyze email message {0}", emailMessage.Id);
                emailMessage.ScoreObject ();
                numAnalyzed++;
            }
            if (0 != numAnalyzed) {
                Log.Info (Log.LOG_BRAIN, "{0} email messages analyzed", numAnalyzed);
            }
            return numAnalyzed;
        }

        private int UpdateEmailAddressScores (int count)
        {
            int numUpdated = 0;
            while (numUpdated < count && !NcApplication.Instance.IsBackgroundAbateRequired &&
                   !EventQueue.Token.IsCancellationRequested) {
                McEmailAddress emailAddress = McEmailAddress.QueryNeedUpdate ();
                if (null == emailAddress) {
                    break;
                }
                emailAddress.Score = emailAddress.GetScore ();
                Log.Debug (Log.LOG_BRAIN, "[McEmailAddress:{0}] update score -> {1:F6}",
                    emailAddress.Id, emailAddress.Score);
                emailAddress.NeedUpdate = false;
                emailAddress.UpdateByBrain ();

                numUpdated++;
            }
            if (0 != numUpdated) {
                Log.Info (Log.LOG_BRAIN, "{0} email address scores updated", numUpdated);
            }
            return numUpdated;
        }

        private int UpdateEmailMessageScores (int count)
        {
            int numUpdated = 0;
            while (numUpdated < count && !NcApplication.Instance.IsBackgroundAbateRequired &&
                   !EventQueue.Token.IsCancellationRequested) {
                McEmailMessage emailMessage = McEmailMessage.QueryNeedUpdate ();
                if (null == emailMessage) {
                    break;
                }
                emailMessage.Score = emailMessage.GetScore ();
                Log.Debug (Log.LOG_BRAIN, "[McEmailMessage:{0}] update score -> {1:F6}",
                    emailMessage.Id, emailMessage.Score);
                emailMessage.NeedUpdate = false;
                emailMessage.UpdateScoreAndNeedUpdate ();
                numUpdated++;
            }
            if (0 != numUpdated) {
                Log.Info (Log.LOG_BRAIN, "{0} email message scores updated", numUpdated);
            }
            return numUpdated;
        }

        private int IndexEmailMessages (int count)
        {
            if (0 == count) {
                return 0;
            }

            int numIndexed = 0;
            long bytesIndexed = 0;
            List<McEmailMessage> emailMessages = McEmailMessage.QueryNeedsIndexing (count);
            Dictionary<int, Index.Index> indexes = new Dictionary<int, Index.Index> ();
            foreach (var emailMessage in emailMessages) {
                if (EventQueue.Token.IsCancellationRequested) {
                    break;
                }

                // If we don't have an index for this account, open one
                Index.Index index;
                if (!indexes.TryGetValue (emailMessage.AccountId, out index)) {
                    var indexPath = NcModel.Instance.GetFileDirPath (emailMessage.AccountId, "index");
                    index = new Index.Index (indexPath);
                    indexes.Add (emailMessage.AccountId, index);
                    index.BeginAddTransaction ();
                }

                // Make sure the body is there
                var messagePath = emailMessage.GetBody ().GetFilePath ();
                if (!File.Exists (messagePath)) {
                    Log.Warn (Log.LOG_BRAIN, "{0} does not exist", messagePath);
                    continue;
                }

                // Index the document
                bytesIndexed +=
                    index.BatchAdd (messagePath, "message", emailMessage.Id.ToString ());
                numIndexed += 1;

                // Mark the email message indexed
                emailMessage.IsIndexed = true;
                emailMessage.UpdateByBrain ();
            }

            foreach (var index in indexes.Values) {
                index.EndAddTransaction ();
                index.Dispose ();
            }
            if (0 != numIndexed) {
                Log.Info (Log.LOG_BRAIN, "{0} email messages indexed", numIndexed);
            }
            if (0 != bytesIndexed) {
                Log.Info (Log.LOG_BRAIN, "{0:N0} bytes indexed", bytesIndexed);
            }
            indexes.Clear ();
            return numIndexed;
        }

        private void ProcessUIEvent (NcBrainUIEvent brainEvent)
        {
            switch (brainEvent.UIType) {  
            case NcBrainUIEventType.MESSAGE_VIEW:
                // Update email and contact statistics
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure ("unknown brain ui event type");
            }
        }

        private void ProcessMessageFlagsEvent (NcBrainMessageFlagEvent brainEvent)
        {
            McEmailMessage emailMessage = McEmailMessage.QueryById<McEmailMessage> ((int)brainEvent.EmailMessageId);
            if (null == emailMessage) {
                return;
            }
            NcAssert.True (emailMessage.AccountId == brainEvent.AccountId);
            emailMessage.UpdateTimeVariance ();
        }

        private void ProcessInitialRicEvent (NcBrainInitialRicEvent brainEvent)
        {
            Log.Debug (Log.LOG_BRAIN, "ProcessInitialRicEvent: accountId={0}", brainEvent.AccountId);
            List<McContact> contactList = McContact.QueryAllRicContacts ((int)brainEvent.AccountId);
            if ((null == contactList) || (0 == contactList.Count)) {
                return;
            }

            /// We normalize all weighted rank by dividing against the maximum weight rank.
            /// QueryAllRicContacts return contacts (desendingly) sorted by weighted rank.
            double maxWeightedRank = (double)contactList [0].WeightedRank;
            foreach (McContact contact in contactList) {
                // Compute the score for all email addresses of the contact
                double score = 0.0;
                if (0 < maxWeightedRank) {
                    score = (double)contact.WeightedRank / maxWeightedRank;
                }
                foreach (McContactEmailAddressAttribute addressAttr in contact.EmailAddresses) {
                    // Find the corresponding McEmailAddress
                    McEmailAddress address = McEmailAddress.QueryById<McEmailAddress> (addressAttr.EmailAddress);
                    if (null == address) {
                        continue;
                    }
                    if (0 < address.ScoreVersion) {
                        /// If Scoreversion > 0, this object has already been scored by
                        /// the real algorithm. So, there is no need for a temporary
                        /// initial score.
                        continue;
                    }

                    // Generate an initial score
                    address.Score = score;
                    address.UpdateByBrain ();
                }
            }
        }

        private void ProcessUpdateAddressEvent (NcBrainUpdateAddressScoreEvent brainEvent)
        {
            Log.Debug (Log.LOG_BRAIN, "ProcessUpdateAddressEvent: event={0}", brainEvent.ToString ());
            McEmailAddress emailAddress =
                McEmailAddress.QueryById<McEmailAddress> ((int)brainEvent.EmailAddressId);
            if (null == emailAddress) {
                return;
            }
            if (Scoring.Version != emailAddress.ScoreVersion) {
                NcAssert.True (Scoring.Version > emailAddress.ScoreVersion);
                return;
            }
            bool updateDependencies = brainEvent.ForceUpdateDependentMessages;
            double newScore = emailAddress.GetScore ();
            if (newScore != emailAddress.Score) {
                emailAddress.Score = newScore;
                emailAddress.UpdateByBrain ();
                updateDependencies = true;
            }
            if (updateDependencies) {
                emailAddress.MarkDependencies ();
            }
        }

        private void ProcessUpdateMessageEvent (NcBrainUpdateMessageScoreEvent brainEvent)
        {
            Log.Debug (Log.LOG_BRAIN, "ProcessUpdateMessageEvent: event={0}", brainEvent.ToString ());
            McEmailMessage emailMessage =
                McEmailMessage.QueryById<McEmailMessage> ((int)brainEvent.EmailMessageId);
            if (null == emailMessage) {
                return;
            }
            if (Scoring.Version != emailMessage.ScoreVersion) {
                NcAssert.True (Scoring.Version > emailMessage.ScoreVersion);
                return;
            }
            double newScore = emailMessage.GetScore ();
            if (newScore != emailMessage.Score) {
                emailMessage.Score = newScore;
                emailMessage.UpdateByBrain ();
            }
        }

        private void ProcessEvent (NcBrainEvent brainEvent)
        {
            Log.Info (Log.LOG_BRAIN, "event type = {0}", Enum.GetName (typeof(NcBrainEventType), brainEvent.Type));

            switch (brainEvent.Type) {
            case NcBrainEventType.PERIODIC_GLEAN:
                if (!NcApplication.Instance.IsBackgroundAbateRequired) {
                    LastPeriodicGlean = DateTime.Now;
                }
                EvaluateRunRate ();
                int num_entries = WorkCredits;
                num_entries -= AnalyzeEmailAddresses (num_entries);
                num_entries -= AnalyzeEmails (num_entries);
                num_entries -= GleanContacts (num_entries);
                num_entries -= UpdateEmailAddressScores (num_entries);
                num_entries -= UpdateEmailMessageScores (num_entries);
                #if INDEXING_ENABLED
                num_entries -= IndexEmailMessages (num_entries);
                #endif
                break;
            case NcBrainEventType.STATE_MACHINE:
                var stateMachineEvent = (NcBrainStateMachineEvent)brainEvent;
                GleanContacts (100, stateMachineEvent.AccountId); /// FIXME - Should get the number from the event arg
                break;
            case NcBrainEventType.UI:
                ProcessUIEvent (brainEvent as NcBrainUIEvent);
                break;
            case NcBrainEventType.MESSAGE_FLAGS:
                ProcessMessageFlagsEvent (brainEvent as NcBrainMessageFlagEvent);
                break;
            case NcBrainEventType.INITIAL_RIC:
                ProcessInitialRicEvent (brainEvent as NcBrainInitialRicEvent);
                break;
            case NcBrainEventType.UPDATE_ADDRESS_SCORE:
                ProcessUpdateAddressEvent (brainEvent as NcBrainUpdateAddressScoreEvent);
                break;
            case NcBrainEventType.UPDATE_MESSAGE_SCORE:
                ProcessUpdateMessageEvent (brainEvent as NcBrainUpdateMessageScoreEvent);
                break;
            case NcBrainEventType.TEST:
                // This is a no op. Serve as a synchronization barrier.
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure ("unknown brain event type");
            }
        }

        public void Process ()
        {
            if (ENABLED) {
                McEmailMessage.StartTimeVariance (EventQueue.Token);
                if (!NcBrain.RegisterStatusIndHandler) {
                    NcApplication.Instance.StatusIndEvent += GenerateInitialContactScores;
                    NcApplication.Instance.StatusIndEvent += UIScrollingEnd;
                    NcApplication.Instance.StatusIndEvent += NewEmailMessageSynced;
                    NcBrain.RegisterStatusIndHandler = true;
                }
            }
            lock (LockObj) {
                while (true) {
                    var brainEvent = EventQueue.Dequeue ();
                    if (NcBrainEventType.TERMINATE == brainEvent.Type) {
                        Log.Info (Log.LOG_BRAIN, "NcBrain Task exits");
                        return;
                    }
                    // TODO - scheduling of brain actions need to be smarter. This will be
                    // addressed in brain 2.0.
                    if (NcApplication.ExecutionContextEnum.QuickSync == NcApplication.Instance.ExecutionContext) {
                        continue;
                    }
                    if (ENABLED) {
                        ProcessEvent (brainEvent);
                    }
                }
            }
        }

        private void NotifyUpdates (NcResult.SubKindEnum type)
        {
            if (!LastNotified.ContainsKey (type)) {
                LastNotified.TryAdd (type, new DateTime ());
            }
            DateTime last;
            bool got = LastNotified.TryGetValue (type, out last);
            NcAssert.True (got);

            // Rate limit to one notification per 2 seconds.
            DateTime now = DateTime.Now;
            if (2000 < (long)(now - last).TotalMilliseconds) {
                LastNotified.TryUpdate (type, now, last);
                StatusIndEventArgs e = new StatusIndEventArgs ();
                e.Account = ConstMcAccount.NotAccountSpecific;
                e.Status = NcResult.Info (type);
                NcApplication.Instance.InvokeStatusIndEvent (e);
            }
        }

        public void NotifyEmailAddressUpdates ()
        {
            NotifyUpdates (NcResult.SubKindEnum.Info_EmailAddressScoreUpdated);
        }

        public void NotifyEmailMessageUpdates ()
        {
            NotifyUpdates (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated);
        }

        public static void NewEmailMessageSynced (object sender, EventArgs args)
        {
            StatusIndEventArgs eventArgs = args as StatusIndEventArgs;
            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged != eventArgs.Status.SubKind) {
                return;
            }
            NcBrain.SharedInstance.Enqueue (new NcBrainStateMachineEvent (eventArgs.Account.Id));
        }

        /// Status indication handler for Info_RicInitialSyncCompleted. We do not 
        /// generate initial email address scores from RIC in this function for 2
        /// reasons. First, we do not want to hold up the status indication callback
        /// for a long duration. Second, the callback may be in a different threads
        /// as NcBrain task. So, we may have two threads updatind the same object.
        /// Therefore, we enqueue a brain event and let brain task to do the actual
        /// processing.
        public static void GenerateInitialContactScores (object sender, EventArgs args)
        {
            StatusIndEventArgs eventArgs = args as StatusIndEventArgs;
            if (NcResult.SubKindEnum.Info_RicInitialSyncCompleted != eventArgs.Status.SubKind) {
                return;
            }
            NcBrain.SharedInstance.Enqueue (new NcBrainInitialRicEvent (eventArgs.Account.Id));
        }

        public void UIScrollingEnd (object sender, EventArgs args)
        {
            StatusIndEventArgs eventArgs = args as StatusIndEventArgs;
            if (NcResult.SubKindEnum.Info_BackgroundAbateStopped != eventArgs.Status.SubKind) {
                return;
            }
            DateTime now = DateTime.Now;
            if (((double)NcContactGleaner.GLEAN_PERIOD < (now - LastPeriodicGlean).TotalSeconds) &&
                (LastPeriodicGleanRestart < LastPeriodicGlean)) {
                LastPeriodicGleanRestart = now;
                NcContactGleaner.Stop ();
                NcContactGleaner.Start ();
            }
        }

        public static void UpdateAddressScore (Int64 emailAddressId, bool forcedUpdateDependentMessages = false)
        {
            NcBrainEvent e = new NcBrainUpdateAddressScoreEvent (emailAddressId, forcedUpdateDependentMessages);
            NcBrain.SharedInstance.Enqueue (e);
        }

        public static void UpdateMessageScore (Int64 emailMessageId)
        {
            NcBrainEvent e = new NcBrainUpdateMessageScoreEvent (emailMessageId);
            NcBrain.SharedInstance.Enqueue (e);
        }
    }
}

