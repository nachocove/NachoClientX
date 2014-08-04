//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Generic;
using System.Threading;
using NachoCore.Utils;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public partial class McEmailMessage : McAbstrItem, IScorable
    {
        [Indexed]
        public int ScoreVersion { get; set; }

        /// Did the user take explicit action?
        public int UserAction { get; set; }

        /// How many times the email is read
        public int TimesRead { get; set; }

        /// How long the user read the email
        public int SecondsRead { get; set; }

        [Indexed] /// Time variance state machine type
        public int TimeVarianceType { get; set; }

        /// Time variance state machine current state
        public int TimeVarianceState { get; set; }

        [Indexed]
        public double Score { get; set; }

        [Indexed]
        public bool NeedUpdate { get; set; }

        [Indexed]
        public bool ScoreIsRead { get; set; }

        [Indexed]
        public bool ScoreIsReplied { get; set; }

        /// If there is update that is not uploaded to the synchronization server,
        /// this object is non-null and holds the update.
        private McEmailMessageScoreSyncInfo SyncInfo { get; set; }

        public double GetScore ()
        {
            double score = 0.0;

            McEmailAddress emailAddress;
            var address = NcEmailAddress.ParseMailboxAddressString (From);
            if (null == address) {
                Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] Cannot parse email address {1}", Id, From);
                return score;
            }
            bool found = McEmailAddress.Get (AccountId, address.Address, out emailAddress);
            if (!found) {
                Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] Unknown email address {1}", Id, From);
                return score;
            }

            // TODO - Combine with content score... once we have such value
            score = emailAddress.GetScore ();
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

        private void ScoreObject_V1 ()
        {
            McEmailAddress emailAddress;
            var address = NcEmailAddress.ParseMailboxAddressString (From);
            if (null != address) {
                bool found = McEmailAddress.Get (AccountId, address.Address, out emailAddress);
                if (found) {
                    // Analyze sender
                    emailAddress.IncrementEmailsReceived ();
                    if (IsRead) {
                        emailAddress.IncrementEmailsRead ();
                    }
                    emailAddress.Score = emailAddress.GetScore ();
                    emailAddress.UpdateByBrain ();

                    // Add Sender dependency
                    McEmailMessageDependency dep = new McEmailMessageDependency ();
                    dep.EmailMessageId = Id;
                    dep.EmailAddressId = emailAddress.Id;
                    dep.EmailAddressType = "Sender";
                    dep.InsertByBrain ();
                } else {
                    Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] Unknown email address {1}", Id, From);
                }
            } else {
                Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] no valid From address ({1})", Id, From);
            }

            ScoreVersion++;
        }

        private bool IsReplied ()
        {
            return (((int)AsLastVerbExecutedType.REPLYTOALL == LastVerbExecuted) ||
            ((int)AsLastVerbExecutedType.REPLYTOSENDER == LastVerbExecuted));
        }

        private void ScoreObject_V2 ()
        {
            McEmailAddress emailAddress;
            var address = NcEmailAddress.ParseMailboxAddressString (From);
            if (null != address) {
                bool found = McEmailAddress.Get (AccountId, address.Address, out emailAddress);
                if (found) {
                    // Migrate EmailsRead count to EmailsReplied when appropriate
                    if (IsReplied ()) {
                        if (IsRead) {
                            emailAddress.IncrementEmailsRead (-1);
                        }
                        emailAddress.IncrementEmailsReplied ();
                        emailAddress.Score = emailAddress.GetScore();
                        emailAddress.UpdateByBrain ();
                    }

                    // Initialize new columns
                    SetScoreIsRead (IsRead);
                    SetScoreIsReplied (IsReplied ());
                } else {
                    Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] Unknown email address {1}", Id, From);
                }
            } else {
                Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] no valid From address ({1})", Id, From);
            }
            ScoreVersion++;
        }  

        public void ScoreObject ()
        {
            NcAssert.True (Scoring.Version > ScoreVersion);
            if (0 == ScoreVersion) {
                ScoreObject_V1 ();
            }
            if (1 == ScoreVersion) {
                ScoreObject_V2 ();
            }
            NcAssert.True (Scoring.Version == ScoreVersion);
            InitializeTimeVariance ();
            Score = GetScore ();
            NeedUpdate = false;
            UpdateByBrain ();
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
            SyncInfo.InsertByBrain ();
        }

        private void ClearScoreSyncInfo ()
        {
            if (null == SyncInfo) {
                return;
            }
            SyncInfo.DeleteByBrain ();
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

        public void SetScoreIsRead (bool value)
        {
            if (value == ScoreIsRead) {
                return;
            }
            ScoreIsRead = value;
            GetScoreSyncInfo ();
            SyncInfo.ScoreIsRead = value;
        }

        public void SetScoreIsReplied (bool value)
        {
            if (value == ScoreIsReplied) {
                return;
            }
            ScoreIsReplied = value;
            GetScoreSyncInfo ();
            SyncInfo.ScoreIsReplied = value;
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
            if (IsValidDateTime (local)) {
                output = local;
            } else if (IsValidDateTime (utc)) {
                output = utc;
            } else {
                return;
            }
        }

        public static McEmailMessage QueryNeedUpdate ()
        {
            return NcModel.Instance.Db.Table<McEmailMessage> ()
                .Where (x => x.NeedUpdate)
                .FirstOrDefault ();
        }

        public static McEmailMessage QueryNeedAnalysis ()
        {
            return NcModel.Instance.Db.Table<McEmailMessage> ()
                .Where (x => x.ScoreVersion < Scoring.Version && x.HasBeenGleaned == true)
                .FirstOrDefault ();
        }

        public static McEmailMessage QueryNeedGleaning ()
        {
            return NcModel.Instance.Db.Table<McEmailMessage> ()
                .Where (x => x.HasBeenGleaned == false)
                .FirstOrDefault ();
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

        /// <summary>
        /// Update the time variance state in memory. Note that the caller is responsible
        /// for calling Update() if this method returns true.
        /// </summary>
        /// <returns><c>true</c>, if time variance was updated, <c>false</c> otherwise.</returns>
        /// <param name="tvList">A list of active time variance.</param>
        /// <param name="now">A timestamp to be used for finding next state for all tv.</param>
        private bool UpdateTimeVarianceStates (NcTimeVariance.TimeVarianceList tvList, DateTime now)
        {
            DateTime latestEvent = new DateTime (1, 1, 1, 0, 0, 0);
            int latestType = (int)NcTimeVarianceType.DONE;
            int latestState = 0;

            foreach (NcTimeVariance tv in tvList) {
                DateTime lastEvent = tv.LastEventTime ();
                if (((int)NcTimeVariance.STATE_NONE == latestType) || (latestEvent < lastEvent)) {
                    latestEvent = lastEvent;
                    latestType = (int)tv.TimeVarianceType ();
                    /// Note that we cannot just use tv.State because tv is not
                    /// running if this function is called from TimerCallBack().
                    /// So, we have to find the appropriate state.
                    latestState = tv.FindNextState (now, -1);
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
            return updated;
        }

        private void InitializeTimeVariance ()
        {
            Log.Debug (Log.LOG_BRAIN, "Initialize time variance for email message id {0}", Id);

            NcAssert.True (1 <= ScoreVersion);

            DateTime now = DateTime.Now;
            NcTimeVariance.TimeVarianceList tvList;
            tvList = EvaluateTimeVariance ().FilterStillRunning (now);

            /// Start all applicable state machines
            if (0 < tvList.Count) {
                foreach (NcTimeVariance tv in tvList) {
                    tv.Start ();
                }
            } else {
                Score = GetScore ();
            }

            if (UpdateTimeVarianceStates (tvList, now)) {
                UpdateByBrain ();
            }
        }

        public void UpdateTimeVariance ()
        {
            NcTimeVariance.StopList (TimeVarianceDescription ());
            InitializeTimeVariance ();
        }

        public void InsertByBrain ()
        {
            int rc = Insert ();
            if (0 < rc) {
                NcBrain brain = NcBrain.SharedInstance;
                brain.McEmailMessageCounters.Insert.Click ();
                brain.NotifyEmailMessageUpdates ();
            }
        }

        public void UpdateByBrain ()
        {
            int rc = Update ();
            if (0 < rc) {
                NcBrain brain = NcBrain.SharedInstance;
                brain.McEmailMessageCounters.Update.Click ();
                brain.NotifyEmailMessageUpdates ();
            }
        }

        public void DeleteByBrain ()
        {
            int rc = Delete ();
            if (0 < rc) {
                NcBrain brain = NcBrain.SharedInstance;
                brain.McEmailMessageCounters.Delete.Click ();
                brain.NotifyEmailMessageUpdates ();
            }
        }

        private static void TimeVarianceCallBack (int state, Int64 objId)
        {
            McEmailMessage emailMessage = McEmailMessage.QueryById<McEmailMessage> ((int)objId);
            if (null == emailMessage) {
                return; // The object has been deleted
            }

            /// Update time variance state if necessary
            DateTime now = DateTime.Now;
            NcTimeVariance.TimeVarianceList tvList =
                emailMessage.EvaluateTimeVariance ().FilterStillRunning (now);
            bool updated = emailMessage.UpdateTimeVarianceStates (tvList, now);

            /// Recompute a new score and update it in the cache
            double newScore = emailMessage.GetScore ();
            if (newScore != emailMessage.Score) {
                emailMessage.Score = newScore;
                updated = true;
            }
            if (updated) {
                emailMessage.NeedUpdate = false;
                emailMessage.UpdateByBrain ();
            }
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
            int n = 0;
            Log.Info (Log.LOG_BRAIN, "Starting all time variances");
            foreach (McEmailMessage emailMessage in emailMessageList) {
                emailMessage.InitializeTimeVariance ();

                /// Throttle
                n = (n + 1) % 8;
                if (0 == n) {
                    Thread.Sleep (new TimeSpan (0, 0, 0, 0, 500));
                }
            }
            Log.Info (Log.LOG_BRAIN, "All time variances started");
        }

        public static void MarkAll ()
        {
            NcModel.Instance.Db.Query<McEmailMessage> ("UPDATE McEmailMessage AS m SET m.NeedUpdate = 1");
        }
    }
}

