//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public partial class McEmailMessage : McItem, IScorable
    {
        // Score version of this object
        public int ScoreVersion { get; set; }

        // How many times the email is read
        public int TimesRead { get; set; }

        // How long the user read the email
        public int SecondsRead { get; set; }

        // If there is update that is not uploaded to the synchronization server,
        // this object is non-null and holds the update.
        private McEmailMessageScoreSyncInfo SyncInfo { get; set; }

        public double GetScore ()
        {
            McContact sender = GetFromContact ();
            NcAssert.True (null != sender);

            // TODO - Combine with content score... once we have such value
            return sender.GetScore ();
        }

        public void ScoreObject ()
        {
            if (0 == ScoreVersion) {
                if (!DownloadScore ()) {
                    // Analyze sender
                    McContact sender = GetFromContact ();
                    sender.IncrementEmailsReceived ();
                    if (IsRead) {
                        sender.IncrementEmailsRead ();
                    }
                    // TODO - How to determine if the email has been replied?
                    sender.Update ();
                }
                ScoreVersion++;
            }
            if (1 == ScoreVersion) {
                // TODO - Analyze thread, content
            }
            NcAssert.True (Scoring.Version == ScoreVersion);
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

