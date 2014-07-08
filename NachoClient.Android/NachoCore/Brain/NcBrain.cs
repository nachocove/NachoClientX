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
        private static NcBrain _SharedInstance;

        public static NcBrain SharedInstance {
            get {
                if (null == _SharedInstance) {
                    _SharedInstance = new NcBrain ();
                }
                return _SharedInstance;
            }
        }

        private NcQueue<NcBrainEvent> EventQueue;

        public NcBrain ()
        {
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
            while (numGleaned < count) {
                // Slow down when the UI is busy
                if (NcApplication.Instance.UiThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId) {
                    NcModel.Instance.RateLimiter.TakeTokenOrSleep ();
                }
                McEmailMessage emailMessage = NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => 
                    x.HasBeenGleaned == false && McItem.BodyStateEnum.Whole_0 == x.BodyState).FirstOrDefault ();
                if (null == emailMessage) {
                    return numGleaned;
                }
                Log.Info (Log.LOG_BRAIN, "glean contact from email message {0}", emailMessage.Id);
                NcContactGleaner.GleanContacts (emailMessage.AccountId, emailMessage);
                numGleaned++;
            }
            return numGleaned;
        }

        private int AnalyzeContacts (int count)
        {
            int numAnalyzed = 0;
            while (numAnalyzed < count) {
                // Slow down when the UI is busy
                if (NcApplication.Instance.UiThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId) {
                    NcModel.Instance.RateLimiter.TakeTokenOrSleep ();
                }
                McContact contact = NcModel.Instance.Db.Table<McContact> ().Where (x => x.ScoreVersion < Scoring.Version).FirstOrDefault ();
                if (null == contact) {
                    return numAnalyzed;
                }
                Log.Info (Log.LOG_BRAIN, "analyze contact {0}", contact.Id);
                contact.ScoreObject ();
                numAnalyzed++;
            }
            return numAnalyzed;
        }

        private int AnalyzeEmails (int count)
        {
            int numAnalyzed = 0;
            while (numAnalyzed < count) {
                // Slow down when the UI is busy
                if (NcApplication.Instance.UiThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId) {
                    NcModel.Instance.RateLimiter.TakeTokenOrSleep ();
                }
                McEmailMessage emailMessage = NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => x.ScoreVersion < Scoring.Version).FirstOrDefault ();
                if (null == emailMessage) {
                    return numAnalyzed;
                }
                Log.Info (Log.LOG_BRAIN, "analyze email message {0}", emailMessage.Id);
                emailMessage.ScoreObject ();
                numAnalyzed++;
            }
            return numAnalyzed;
        }

        private int UpdateContactScores (int count)
        {
            int numUpdated = 0;
            while (numUpdated < count) {
                // Slow down when the UI is busy
                if (NcApplication.Instance.UiThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId) {
                    NcModel.Instance.RateLimiter.TakeTokenOrSleep ();
                }
                McContact contact = NcModel.Instance.Db.Table<McContact> ().Where (x => x.NeedUpdate).FirstOrDefault ();
                if (null == contact) {
                    return numUpdated;
                }
                Log.Info (Log.LOG_BRAIN, "update score of contact {0}", contact.Id);
                contact.Score = contact.GetScore ();
                contact.NeedUpdate = false;
                contact.Update ();

                numUpdated++;
            }
            return numUpdated;
        }

        private int UpdateEmailMessageScores (int count)
        {
            int numUpdated = 0;
            while (numUpdated < count) {
                // Slow down when the UI is busy
                if (NcApplication.Instance.UiThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId) {
                    NcModel.Instance.RateLimiter.TakeTokenOrSleep ();
                }
                McEmailMessage emailMessage = NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => x.NeedUpdate).FirstOrDefault ();
                if (null == emailMessage) {
                    return numUpdated;
                }
                Log.Info (Log.LOG_BRAIN, "update score of email message {0}", emailMessage.Id);
                emailMessage.Score = emailMessage.GetScore ();
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
                const int NUM_ENTRIES = 100;
                if (NUM_ENTRIES == GleanContacts (NUM_ENTRIES)) {
                    break;
                }
                if (NUM_ENTRIES == AnalyzeContacts (NUM_ENTRIES)) {
                    break;
                }
                if (NUM_ENTRIES == AnalyzeEmails (NUM_ENTRIES)) {
                    break;
                }
                if (NUM_ENTRIES == UpdateContactScores (NUM_ENTRIES)) {
                    break;
                }
                if (NUM_ENTRIES == UpdateEmailMessageScores (NUM_ENTRIES)) {
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
                ProcessEvent (brainEvent);
                brainEvent = EventQueue.Dequeue ();
            }
        }
    }
}

