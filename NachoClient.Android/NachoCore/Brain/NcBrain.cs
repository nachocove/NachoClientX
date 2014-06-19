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

        private Task TaskHandle;
        private NcQueue<NcBrainEvent> EventQueue;

        public NcBrain ()
        {
            EventQueue = new NcQueue<NcBrainEvent> ();
            TaskHandle = NcTask.Run (Process, "Brain");
        }

        public void Enqueue (NcBrainEvent brainEvent)
        {
            EventQueue.Enqueue (brainEvent);
        }

        private void GleanContacts (int count)
        {
            // Look for a list of emails 
            while (0 < count) {
                McEmailMessage emailMessage = NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => x.HasBeenGleaned == false).FirstOrDefault ();
                if (null == emailMessage) {
                    return;
                }
                NcContactGleaner.GleanContacts (emailMessage.AccountId, emailMessage);
                count--;
            }
        }

        private void AnalyzeEmails (int count)
        {
            while (0 < count) {
                McEmailMessage emailMessage = NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => x.ScoreVersion < Scoring.Version).FirstOrDefault ();
                if (null == emailMessage) {
                    return;
                }
                emailMessage.ScoreObject ();
                count--;
            }
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
            Log.Debug (Log.LOG_BRAIN, "event type = {0}", Enum.GetName (typeof(NcBrainEventType), brainEvent.Type));

            switch (brainEvent.Type) {
            case NcBrainEventType.PERIODIC_GLEAN:
                GleanContacts (100);
                AnalyzeEmails (100);
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

