//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public partial class McEmailMessage : McItem, IScorable
    {
        public int ScoreVersion { get; set; }

        /// Did the user take explicit action?
        public int UserAction { get; set; }

        /// How many times the email is read
        public int TimesRead { get; set; }

        /// How long the user read the email
        public int SecondsRead { get; set; }

        /// Time variance state machine type
        public int TimeVarianceType { get; set; }

        /// Time variance state machine current state
        public int TimeVarianceState { get; set; }

        public double Score { get; set; }

        public bool NeedUpdate { get; set; }

        /// If there is update that is not uploaded to the synchronization server,
        /// this object is non-null and holds the update.
        private McEmailMessageScoreSyncInfo SyncInfo { get; set; }

        public double GetScore ()
        {
            double score = 0.0;

            McContact sender = GetFromContact ();
            if (null == sender) {
                return score;
            }

            // TODO - Combine with content score... once we have such value
            score = sender.GetScore ();
            NcTimeVariance.TimeVarianceList tvList = EvaluateTimeVariance ();
            if (0 < tvList.Count) {
                DateTime now = DateTime.Now;
                foreach (NcTimeVariance tv in tvList) {
                    score *= tv.Adjustment (now);
                }
            }
            Log.Debug (Log.LOG_BRAIN, "[McEmailMessage:{0}]: score = {1:F6}", Id, score);
            return score;
        }

        public void ScoreObject ()
        {
            NcAssert.True (Scoring.Version > ScoreVersion);
            if (0 == ScoreVersion) {
                McContact sender = GetFromContact ();
                if (null != sender) {
                    if (!DownloadScore ()) {
                        // Analyze sender
                        sender.IncrementEmailsReceived ();
                        if (IsRead) {
                            sender.IncrementEmailsRead ();
                        }
                        // TODO - How to determine if the email has been replied?
                        sender.ForceReadAncillaryData ();
                        sender.Update ();
                    }
                    // Add Sender dependency
                    McEmailMessageDependency dep = new McEmailMessageDependency ();
                    dep.EmailMessageId = Id;
                    dep.ContactId = sender.Id;
                    dep.ContactType = "Sender";
                    dep.Insert ();
                }

                ScoreVersion++;
            }
            if (1 == ScoreVersion) {
                // TODO - Analyze thread, content
            }
            NcAssert.True (Scoring.Version == ScoreVersion);
            InitializeTimeVariance ();
            Score = GetScore ();
            NeedUpdate = false;
            Update ();
        }

        private void GetScoreSyncInfo ()
        {
            if (null != SyncInfo) {
                return;
            }
            SyncInfo = NcModel.Instance.Db.Table<McEmailMessageScoreSyncInfo> ().Where (x => x.EmailMessageId == Id).FirstOrDefault ();
            if (null != SyncInfo) {
                return;
            }
            SyncInfo = new McEmailMessageScoreSyncInfo ();
            SyncInfo.EmailMessageId = Id;
            SyncInfo.Insert ();
        }

        private void ClearScoreSyncInfo ()
        {
            if (null == SyncInfo) {
                return;
            }
            SyncInfo.Delete ();
            SyncInfo = null;
        }

        public void IncrementTimesRead (int count = 1)
        {
            TimesRead += count;
            GetScoreSyncInfo ();
            SyncInfo.TimesRead += count;
        }

        public void IncrementSecondsRead (int seconds)
        {
            SecondsRead += seconds;
            GetScoreSyncInfo ();
            SyncInfo.SecondsRead += seconds;
        }

        public void UploadScore ()
        {
            Log.Debug (Log.LOG_BRAIN, "email message id = {0}", Id);
            if (null != SyncInfo) {
                // TODO - Add real implementation. Currently, just clear the delta
                ClearScoreSyncInfo ();
            }
        }

        public bool DownloadScore ()
        {
            Log.Debug (Log.LOG_BRAIN, "email message id = {0}", Id);
            return false;
        }

        private string TimeVarianceDescription ()
        {
            return String.Format ("[McEmailMessage:{0}]", Id);
        }

        /// There is no nullable type in db. So, use DateTime.MinValue to indicate
        /// the absence of a value.
        private bool IsValidDateTime (DateTime dt)
        {
            return (DateTime.MinValue != dt);
        }

        private void ExtractDateTimeFromPair (DateTime local, DateTime utc, ref DateTime output)
        {
            DateTime time;
            if (IsValidDateTime (local)) {
                time = local;
            } else if (IsValidDateTime (utc)) {
                time = utc;
            } else {
                return;
            }
            if (time > DateTime.Now) {
                output = time;
            }
        }

        /// <summary>
        /// Evaluate the parameters in McEmailMessage and produce a list of 
        /// NcTimeVariance that applies. These NcTimeVariance do not need to be 
        /// running. They just need to exist at point given the email parameters.
        /// </summary>
        /// <returns>List of NcTimeVariance objects</returns>
        private NcTimeVariance.TimeVarianceList EvaluateTimeVariance ()
        {
            NcTimeVariance.TimeVarianceList tvList = new NcTimeVariance.TimeVarianceList ();
            DateTime deadline = DateTime.MinValue;
            DateTime deferredUntil = DateTime.MinValue;

            ExtractDateTimeFromPair (FlagDeferUntil, FlagUtcDeferUntil, ref deferredUntil);
            ExtractDateTimeFromPair (FlagDue, FlagUtcDue, ref deadline);

            if (IsValidDateTime (deadline)) {
                NcDeadlineTimeVariance tv =
                    new NcDeadlineTimeVariance (TimeVarianceDescription (), TimeVarianceCallBack, Id, deadline);
                tvList.Add (tv);
            }
            if (IsValidDateTime (deferredUntil)) {
                NcDeferenceTimeVariance tv =
                    new NcDeferenceTimeVariance (TimeVarianceDescription (), TimeVarianceCallBack, Id, deferredUntil);
                tvList.Add (tv);
            }
            {
                NcAgingTimeVariance tv =
                    new NcAgingTimeVariance (TimeVarianceDescription (), TimeVarianceCallBack, Id, DateReceived);
                tvList.Add (tv);
            }

            return tvList;
        }

        /// Return a list of time variance state machines that are still running.
        /// Used for starting state machines and for determine time variance state
        private List<NcTimeVariance> FilterTimeVariance (List<NcTimeVariance> tvList, DateTime now)
        {
            List<NcTimeVariance> newTvList = new List<NcTimeVariance> ();
            if ((null == tvList) || (0 < tvList.Count)) {
                return newTvList;
            }
            foreach (NcTimeVariance tv in tvList) {
                DateTime lastEvent = tv.LastEventTime ();
                if (lastEvent <= now) {
                    continue;
                }
                newTvList.Add (tv);
            }
            return newTvList;
        }

        private void UpdateTimeVariance (NcTimeVariance.TimeVarianceList tvList)
        {
            DateTime latestEvent = new DateTime (1, 1, 1, 0, 0, 0);
            int latestType = (int)NcTimeVarianceType.NONE;
            int latestState = 0;

            foreach (NcTimeVariance tv in tvList) {
                DateTime lastEvent = tv.LastEventTime ();
                if (((int)NcTimeVariance.STATE_NONE == latestType) || (latestEvent < lastEvent)) {
                    latestEvent = lastEvent;
                    latestType = (int)tv.TimeVarianceType ();
                    latestState = tv.State;
                }
            }

            /// Update db state only if there is a change
            bool updated = false;
            if (latestType != TimeVarianceType) {
                TimeVarianceType = latestType;
                updated = true;
            }
            if (latestState != TimeVarianceState) {
                TimeVarianceState = latestState;
                updated = true;
            }
            if (updated) {
                Update ();
            }
        }

        private void InitializeTimeVariance ()
        {
            Log.Debug (Log.LOG_BRAIN, "Initialize time variance for email message id {0}", Id);

            NcAssert.True (1 <= ScoreVersion);

            DateTime now = DateTime.Now;
            NcTimeVariance.TimeVarianceList tvList;
            tvList = EvaluateTimeVariance ().FilterStillRunning (now);

            /// Start all applicable state machines
            foreach (NcTimeVariance tv in tvList) {
                tv.Start ();
            }

            UpdateTimeVariance (tvList);
        }

        private static void TimeVarianceCallBack (int state, Int64 objId)
        {
            McEmailMessage emailMessage = McEmailMessage.QueryById<McEmailMessage> ((int)objId);
            if (null == emailMessage) {
                return; // The object has been deleted
            }

            /// Update time variance state if necessary
            NcTimeVariance.TimeVarianceList tvList =
                emailMessage.EvaluateTimeVariance ().FilterStillRunning (DateTime.Now);
            emailMessage.UpdateTimeVariance (tvList);

            /// Recompute a new score and update it in the cache
            emailMessage.Score = emailMessage.GetScore ();
            emailMessage.NeedUpdate = false;
            emailMessage.Update ();
        }

        public static void StartTimeVariance ()
        {
            /// Look for all email messages that are:
            ///
            // 1. ScoreVersion is non-zero
            // 2. TimeVarianceType is not DONE
            List<McEmailMessage> emailMessageList =
                NcModel.Instance.Db.Query<McEmailMessage> ("SELECT * FROM McEmailMessage AS m " +
                "WHERE m.ScoreVersion > 0 AND m.TimeVarianceType != ?", NcTimeVarianceType.DONE);
            foreach (McEmailMessage emailMessage in emailMessageList) {
                emailMessage.InitializeTimeVariance ();
            }
        }

        public static void MarkAll ()
        {
            NcModel.Instance.Db.Query<McEmailMessage> ("UPDATE McEmailMessage AS m SET m.NeedUpdate = 1");
        }
    }

    public class McEmailMessageScoreSyncInfo : McObject
    {
        // Id of the corresponding McEmailMessage
        [Indexed]
        public Int64 EmailMessageId { get; set; }

        // How many times the email is read
        public int TimesRead { get; set; }

        // How long the user read the email
        public int SecondsRead { get; set; }

        public McEmailMessageScoreSyncInfo ()
        {
            EmailMessageId = 0;
            TimesRead = 0;
            SecondsRead = 0;
        }
    }
}

