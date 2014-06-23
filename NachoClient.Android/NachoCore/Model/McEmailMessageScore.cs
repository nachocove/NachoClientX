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
            McContact sender = GetFromContact ();
            NcAssert.True (null != sender);

            // TODO - Combine with content score... once we have such value
            return sender.GetScore ();
        }

        private void UpdateScore ()
        {
            Score = GetScore ();
            Log.Debug (Log.LOG_BRAIN, "email message {0}: score=%f", Id, Score);
            NeedUpdate = false;
            Update ();
        }

        public void ScoreObject ()
        {
            if (0 == ScoreVersion) {
                McContact sender = GetFromContact ();
                if (!DownloadScore ()) {
                    // Analyze sender
                    sender.IncrementEmailsReceived ();
                    if (IsRead) {
                        sender.IncrementEmailsRead ();
                    }
                    // TODO - How to determine if the email has been replied?
                    sender.Update ();
                }
                // Add Sender dependency
                McEmailMessageDependency dep = new McEmailMessageDependency ();
                dep.EmailMessageId = Id;
                dep.ContactId = sender.Id;
                dep.ContactType = "Sender";
                dep.Insert ();

                ScoreVersion++;
            }
            if (1 == ScoreVersion) {
                // TODO - Analyze thread, content
            }
            NcAssert.True (Scoring.Version == ScoreVersion);
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

        public void IncrementTimesRead (int count=1)
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
            return String.Format ("<emailMessage: {0}>", Id);
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

        private void InitializeTimeVariance ()
        {
            DateTime deadline = DateTime.MinValue;
            DateTime deferredUntil = DateTime.MinValue;
            NcTimeVariance tv = null;

            ExtractDateTimeFromPair (FlagDeferUntil, FlagUtcDeferUntil, ref deferredUntil);
            ExtractDateTimeFromPair (FlagDue, FlagUtcDue, ref deadline);
            if (!IsValidDateTime (deadline) && !IsValidDateTime (deferredUntil)) {
                // Default to aging unless we are already running that type of time variance
                if (NcTimeVarianceType.AGING != (NcTimeVarianceType)TimeVarianceType) {
                    TimeVarianceType = (int)NcTimeVarianceType.AGING;
                    TimeVarianceState = 1;
                    tv = new NcAgingTimeVariance (TimeVarianceDescription (), DateReceived);
                } else {
                    TimeVarianceType = (int)NcTimeVarianceType.DONE;
                    TimeVarianceState = 0;
                }
            } else {
                if (!IsValidDateTime (deferredUntil)) {
                    /// Deadline only
                    TimeVarianceType = (int)NcTimeVarianceType.DEADLINE;
                    tv = new NcDeadlineTimeVariance (TimeVarianceDescription (), deadline);
                } else if (!IsValidDateTime (deadline)) {
                    /// Deference only
                    TimeVarianceType = (int)NcTimeVarianceType.DEFERENCE;
                    tv = new NcDeadlineTimeVariance (TimeVarianceDescription (), deferredUntil);
                } else if (deadline < deferredUntil) {
                    /// Deadline first
                    TimeVarianceType = (int)NcTimeVarianceType.DEADLINE;
                    tv = new NcDeadlineTimeVariance (TimeVarianceDescription (), deadline);
                } else {
                    /// Deference first
                    TimeVarianceType = (int)NcTimeVarianceType.DEFERENCE;
                    tv = new NcDeferenceTimeVariance (TimeVarianceDescription (), deferredUntil);
                }
                TimeVarianceState = 1;
            }
            if (null != tv) {
                Update ();
                tv.CallBack = TimeVarianceCallBack;
                tv.Start ();
            }
        }

        private void TimeVarianceCallBack (int state)
        {
            if (0 == state) {
                /// Check if there is any other time variance state machine to run
                InitializeTimeVariance ();
            } else {
                /// Recompute a new score and update it in the cache
                Score = GetScore ();
                NeedUpdate = false;
                Update ();
            }
        }

        public static void StartTimeVariance ()
        {
            /// Look for all email messages that are:
            ///
            // 1. ScoreVersion is non-zero
            // 2. TimeVarianceType is non-zero
            // 3. TimeVarianceState is non-zero
            List<McEmailMessage> emailMessageList =
                NcModel.Instance.Db.Query<McEmailMessage> ("SELECT * FROM McEmailMessage AS m " +
                "WHERE m.ScoreVersion > 0 AND m.TimeVarianceType != 0 AND m.TimeVarianceState != 0");
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

