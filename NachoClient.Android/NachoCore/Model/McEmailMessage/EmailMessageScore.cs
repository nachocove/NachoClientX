//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NachoCore.Utils;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public partial class McEmailMessage : McAbstrItem, IScorable
    {
        public enum GleanPhaseEnum
        {
            NOT_GLEANED = 0,
            GLEAN_PHASE1 = 1,
            GLEAN_PHASE2 = 2,
        };

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

        /// This field is depracated and should not be used.
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

        public const double VipScore = 1.0;

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
            if (emailAddress.IsVip) {
                score = VipScore;
            } else if (0 < UserAction) {
                score = VipScore;
            } else {
                score = emailAddress.GetScore ();
                NcTimeVariance.TimeVarianceList tvList = EvaluateTimeVariance ();
                if (0 < tvList.Count) {
                    DateTime now = DateTime.UtcNow;
                    foreach (NcTimeVariance tv in tvList) {
                        score *= tv.Adjustment (now);
                    }
                }
                if (0 > UserAction) {
                    if (minHotScore <= score) {
                        score = minHotScore - 0.01;
                    }
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
                    McEmailMessageDependency dep = new McEmailMessageDependency (AccountId);
                    dep.EmailMessageId = Id;
                    dep.EmailAddressId = emailAddress.Id;
                    dep.EmailAddressType = (int)McEmailMessageDependency.AddressType.SENDER;
                    dep.InsertByBrain ();
                } else {
                    Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] Unknown email address", Id);
                }
            } else {
                Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] no valid From address", Id);
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
                        emailAddress.Score = emailAddress.GetScore ();
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

        private void ScoreObject_V3 ()
        {
            // No statistics is updated. Just need to re-compute the score which
            // will be done at the end of ScoreObject().
            ScoreVersion++;
            NcAssert.True (3 == ScoreVersion);
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
            if (2 == ScoreVersion) {
                ScoreObject_V3 ();
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
            SyncInfo.AccountId = AccountId;
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
                .Where (x => x.ScoreVersion < Scoring.Version && x.HasBeenGleaned == 0)
                .FirstOrDefault ();
        }

        public static List<McEmailMessage> QueryNeedGleaning (Int64 accountId, int count)
        {
            var query = String.Format ("SELECT e.* FROM McEmailMessage AS e " +
                        " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                        " WHERE likelihood (HasBeenGleaned < ?, 0.1) " +
                        "  AND likelihood (m.FolderId NOT IN {0}, 0.9) ", McFolder.JunkFolderListSqlString ());
            if (0 <= accountId) {
                query += " AND likelihood (e.AccountId = ?, 1.0) LIMIT ?";
                return NcModel.Instance.Db.Query<McEmailMessage> (query, GleanPhaseEnum.GLEAN_PHASE2, accountId, count);
            } else {
                query += " LIMIT ?";
                return NcModel.Instance.Db.Query<McEmailMessage> (query, GleanPhaseEnum.GLEAN_PHASE2, count);
            }
        }

        public static int CountByVersion (int version)
        {
            return NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => x.ScoreVersion == version).Count ();
        }

        public static int Count ()
        {
            return NcModel.Instance.Db.Table<McEmailMessage> ().Count ();
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

            if (IsMeetingInvite (out deadline)) {
                // Meeting invite is a special case. If the email message is a
                // meeting invite with a valid stop time, a single deadline time
                // variance state machine is created. No other consideration is
                // needed.
                NcMeetingTimeVariance tv = 
                    new NcMeetingTimeVariance (TimeVarianceDescription (), TimeVarianceCallBack, Id, deadline);
                tvList.Add (tv);
                return tvList;
            }

            ExtractDateTimeFromPair (FlagStartDate, FlagUtcStartDate, ref deferredUntil);
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

        /// <summary>
        /// Update the time variance state in memory. Note that the caller is responsible
        /// for calling Update() if this method returns true.
        /// </summary>
        /// <returns><c>true</c>, if time variance was updated, <c>false</c> otherwise.</returns>
        /// <param name="tvList">A list of active time variance.</param>
        /// <param name="now">A timestamp to be used for finding next state for all tv.</param>
        private bool UpdateTimeVarianceStates (NcTimeVariance.TimeVarianceList tvList, DateTime now)
        {
            // If the list of active time variances is empty, then the message's score is
            // stable.  This is indicated by a TimeVarianceType value of DONE.
            //
            // TODO The TimeVarianceType field used to be used for something else.  It has
            // been repurposed for now, and is used like a boolean field.  The repurposing
            // was done to avoid changing the database table layout.  This should be cleaned
            // up in the Brain 2.0 work.

            bool updated = false;
            if (0 == tvList.Count && (int)NcTimeVarianceType.DONE != TimeVarianceType) {
                TimeVarianceType = (int)NcTimeVarianceType.DONE;
                updated = true;
            }
            if (0 < tvList.Count && (int)NcTimeVarianceType.DONE == TimeVarianceType) {
                TimeVarianceType = (int)NcTimeVarianceType.NONE;
                updated = true;
            }
            return updated;
        }

        private void InitializeTimeVariance ()
        {
            Log.Debug (Log.LOG_BRAIN, "Initialize time variance for email message id {0}", Id);

            if (0 == ScoreVersion) {
                return;
            }

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

        public void UpdateScoreAndNeedUpdate ()
        {
            int rc = NcModel.Instance.BusyProtect (() => {
                return NcModel.Instance.Db.Execute (
                    "UPDATE McEmailMessage " +
                    "SET Score = ?,  NeedUpdate = ? " +
                    "WHERE Id = ?", Score, NeedUpdate, Id);
            });
            if (0 < rc) {
                NcBrain brain = NcBrain.SharedInstance;
                brain.McEmailMessageCounters.Update.Click ();
                brain.NotifyEmailMessageUpdates ();
            }
        }

        public void MarkAsGleaned (GleanPhaseEnum phase)
        {
            HasBeenGleaned = (int)phase;
            Update ();
        }

        private static void TimeVarianceCallBack (int state, Int64 objId)
        {
            McEmailMessage emailMessage = McEmailMessage.QueryById<McEmailMessage> ((int)objId);
            if (null == emailMessage) {
                return; // The object has been deleted
            }

            // Update time variance state if necessary
            DateTime now = DateTime.UtcNow;
            NcTimeVariance.TimeVarianceList tvList =
                emailMessage.EvaluateTimeVariance ().FilterStillRunning (now);
            bool fullUpdateNeeded = emailMessage.UpdateTimeVarianceStates (tvList, now);

            // Recompute a new score and update it in the cache
            bool scoreChanged = false;
            double newScore = emailMessage.GetScore ();
            if (newScore != emailMessage.Score) {
                emailMessage.Score = newScore;
                scoreChanged = true;
            }
            if (fullUpdateNeeded || scoreChanged) {
                emailMessage.NeedUpdate = false;
                if (fullUpdateNeeded) {
                    emailMessage.UpdateByBrain ();
                } else {
                    emailMessage.UpdateScoreAndNeedUpdate ();
                }
            }
        }

        public static void StartTimeVariance (CancellationToken token)
        {
            /// Look for all email messages that are:
            ///
            // 1. ScoreVersion is non-zero
            // 2. TimeVarianceType is not DONE
            List<NcEmailMessageIndex> emailMessageIdList = 
                NcModel.Instance.Db.Query<NcEmailMessageIndex> ("SELECT m.Id FROM McEmailMessage AS m " +
                "WHERE m.ScoreVersion > 0 AND m.TimeVarianceType != ? ORDER BY DateReceived ASC", NcTimeVarianceType.DONE);
            int n = 0;
            int numStarted = 0;
            Log.Info (Log.LOG_BRAIN, "Starting all time variances");
            foreach (var emailMessageId in emailMessageIdList) {
                var emailMessage = McEmailMessage.QueryById<McEmailMessage> (emailMessageId.Id);
                if (null == emailMessage) {
                    continue;
                }
                emailMessage.UpdateTimeVariance ();
                numStarted++;

                /// Throttle
                n = (n + 1) % 8;
                if (0 == n) {
                    if (!NcTask.CancelableSleep (500, token)) {
                        break;
                    }
                }
            }
            Log.Info (Log.LOG_BRAIN, "{0} time variances started", numStarted);
        }

        public static void MarkAll ()
        {
            NcModel.Instance.Db.Query<McEmailMessage> ("UPDATE McEmailMessage AS m SET m.NeedUpdate = 1");
        }

    }
}

