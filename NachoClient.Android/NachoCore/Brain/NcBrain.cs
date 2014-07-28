//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading.Tasks;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public class NcBrain
    {
        public const bool ENABLED = true;

        private static NcBrain _SharedInstance;

        public static NcBrain SharedInstance {
            get {
                if (null == _SharedInstance) {
                    _SharedInstance = new NcBrain ();
                }
                return _SharedInstance;
            }
        }

        public class OperationCounters {
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

        public NcBrain ()
        {
            RootCounter = new NcCounter ("Brain", true);
            McEmailMessageCounters = new OperationCounters ("McEmailMessage", RootCounter);
            McEmailMessageDependencyCounters = new OperationCounters ("McEmailMessageDependency", RootCounter);
            McEmailMessageScoreSyncInfoCounters = new OperationCounters ("McEmailMessageScoreSyncInfo", RootCounter);
            McEmailAddressCounters = new OperationCounters ("McEmailAddress", RootCounter);
            McEmailAddressScoreSyncInfo = new OperationCounters ("McEmailAddressScoreSyncInfo", RootCounter);
            RootCounter.AutoReset = true;
            RootCounter.ReportPeriod = 60 * 60; // report once per hour

            EventQueue = new NcQueue<NcBrainEvent> ();
            NcTask.Run (() => {
                EventQueue.Token = NcTask.Cts.Token;
                Process ();
            }, "Brain");
        }

        public void Enqueue (NcBrainEvent brainEvent)
        {
            EventQueue.Enqueue (brainEvent);
        }

        private int GleanContacts (int count)
        {
            // Look for a list of emails
            int numGleaned = 0;
            while (numGleaned < count && !NcApplication.Instance.IsBackgroundAbateRequired) {
                McEmailMessage emailMessage = NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => 
                    x.HasBeenGleaned == false && McAbstrItem.BodyStateEnum.Whole_0 == x.BodyState).FirstOrDefault ();
                if (null == emailMessage) {
                    return numGleaned;
                }
                Log.Info (Log.LOG_BRAIN, "glean contact from email message {0}", emailMessage.Id);
                NcContactGleaner.GleanContacts (emailMessage.AccountId, emailMessage);
                numGleaned++;
            }
            return numGleaned;
        }

        private int AnalyzeEmailAddresses (int count)
        {
            int numAnalyzed = 0;
            while (numAnalyzed < count && !NcApplication.Instance.IsBackgroundAbateRequired) {
                McEmailAddress emailAddress = NcModel.Instance.Db.Table<McEmailAddress> ().Where (x => x.ScoreVersion < Scoring.Version).FirstOrDefault ();
                if (null == emailAddress) {
                    break;
                }
                Log.Info (Log.LOG_BRAIN, "analyze email address {0}", emailAddress.Id);
                emailAddress.ScoreObject ();
                numAnalyzed++;
            }
            Log.Info (Log.LOG_BRAIN, "{0} email addresses analyzed", numAnalyzed);
            return numAnalyzed;
        }

        private int AnalyzeEmails (int count)
        {
            int numAnalyzed = 0;
            while (numAnalyzed < count && !NcApplication.Instance.IsBackgroundAbateRequired) {
                McEmailMessage emailMessage = NcModel.Instance.Db.Table<McEmailMessage> ()
                    .Where (x => x.ScoreVersion < Scoring.Version && x.HasBeenGleaned == true)
                    .FirstOrDefault ();
                if (null == emailMessage) {
                    break;
                }
                Log.Debug (Log.LOG_BRAIN, "analyze email message {0}", emailMessage.Id);
                emailMessage.ScoreObject ();
                numAnalyzed++;
            }
            Log.Info (Log.LOG_BRAIN, "{0} email messages analyzed", numAnalyzed);
            return numAnalyzed;
        }

        private int UpdateEmailAddressScores (int count)
        {
            int numUpdated = 0;
            while (numUpdated < count && !NcApplication.Instance.IsBackgroundAbateRequired) {
                McEmailAddress emailAddress = NcModel.Instance.Db.Table<McEmailAddress> ().Where (x => x.NeedUpdate).FirstOrDefault ();
                if (null == emailAddress) {
                    return numUpdated;
                }
                emailAddress.Score = emailAddress.GetScore ();
                Log.Debug (Log.LOG_BRAIN, "[McEmailAddress:{0}] update score -> {1:F6}",
                    emailAddress.Id, emailAddress.Score);
                emailAddress.NeedUpdate = false;
                emailAddress.Update ();

                numUpdated++;
            }
            return numUpdated;
        }

        private int UpdateEmailMessageScores (int count)
        {
            int numUpdated = 0;
            while (numUpdated < count  && !NcApplication.Instance.IsBackgroundAbateRequired) {
                McEmailMessage emailMessage = NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => x.NeedUpdate).FirstOrDefault ();
                if (null == emailMessage) {
                    return numUpdated;
                }
                emailMessage.Score = emailMessage.GetScore ();
                Log.Debug (Log.LOG_BRAIN, "[McEmailMessage:{0}] update score -> {1:F6}",
                    emailMessage.Id, emailMessage.Score);
                emailMessage.NeedUpdate = false;
                emailMessage.Update ();

                numUpdated++;
            }
            return numUpdated;
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

        private void ProcessEvent (NcBrainEvent brainEvent)
        {
            Log.Info (Log.LOG_BRAIN, "event type = {0}", Enum.GetName (typeof(NcBrainEventType), brainEvent.Type));

            switch (brainEvent.Type) {
            case NcBrainEventType.PERIODIC_GLEAN:
                int num_entries = 30;
                num_entries -= GleanContacts (num_entries);
                if (0 >= num_entries) {
                    break;
                }
                num_entries -= AnalyzeEmailAddresses (num_entries);
                if (0 >= num_entries) {
                    break;
                }
                num_entries -= AnalyzeEmails (num_entries);
                if (0 >= num_entries) {
                    break;
                }
                num_entries -= UpdateEmailAddressScores (num_entries);
                if (0 >= num_entries) {
                    break;
                }
                num_entries -= UpdateEmailMessageScores (num_entries);
                if (0 >= num_entries) {
                    break;
                }
                break;
            case NcBrainEventType.STATE_MACHINE:
                GleanContacts (int.MaxValue);
                AnalyzeEmails (int.MaxValue);
                break;
            case NcBrainEventType.UI:
                ProcessUIEvent (brainEvent as NcBrainUIEvent);
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure ("unknown brain event type");
            }
        }

        private void Process ()
        {
            McEmailMessage.StartTimeVariance ();
            while (true) {
                var brainEvent = EventQueue.Dequeue ();
                if (NcBrainEventType.TERMINATE == brainEvent.Type) {
                    return;
                }
                if (ENABLED) {
                    ProcessEvent (brainEvent);
                }
            }
        }
    }
}

