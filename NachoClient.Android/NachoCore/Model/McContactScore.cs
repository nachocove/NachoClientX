//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Xml.Linq;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public partial class McContact : McItem, IScorable
    {   
        // Score version of this object
        public int ScoreVersion { get; set; }

        // DO NOT update these fields directly. Use IncrementXXX methods instead.
        // Otherwise, the delta will not be saved correctly. ORM does not allow
        // private property so there is no way to use a public property with 
        // customized getters to read of a private property.

        // Number of emails receivied
        public int EmailsReceived { get; set; }

        // Number of emails read
        public int EmailsRead { get; set; }

        // Number of emails replied
        public int EmailsReplied { get; set; }

        // Number of emails archived
        public int EmailsArchived { get; set; }

        // Number of emails sent to this contact
        public int EmailsSent { get; set; }

        // Number of emails deleted without being read
        public int EmailsDeleted { get; set; }

        // If true, the score is 1.0
        public bool IsVip { get; set; }

        // If there is update that is not uploaded to the synchronization server,
        // this object is non-null and holds the update.
        private McContactScoreSyncInfo SyncInfo { get; set; }

        public double GetScore ()
        {
            if (IsVip) {
                return 1.0; // 100% sure. User says so.
            }

            int total = EmailsReceived + EmailsSent + EmailsDeleted;
            if (0 == total) {
                return 0.0;
            }
            return (double)(EmailsRead + EmailsReplied + EmailsSent) / (double)total;
        }

        public void ScoreObject ()
        {
            if (0 == ScoreVersion) {
                if (!DownloadScore ()) {
                    // Version 1 statistics are updated by emails. Nothing to do here
                }
                ScoreVersion++;
            }
            NcAssert.True (Scoring.Version == ScoreVersion);
        }

        private void GetScoreSyncInfo ()
        {
            if (null != SyncInfo) {
                return;
            }
            SyncInfo = NcModel.Instance.Db.Table<McContactScoreSyncInfo> ().Where (x => x.ContactId == Id).FirstOrDefault ();
            if (null != SyncInfo) {
                return;
            }
            SyncInfo = new McContactScoreSyncInfo ();
            SyncInfo.ContactId = Id;
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

        public void IncrementEmailsReceived (int count=1)
        {
            EmailsReceived += count;
            GetScoreSyncInfo ();
            SyncInfo.EmailsReceived += count;
        }

        public void IncrementEmailsRead (int count=1)
        {
            EmailsRead += count;
            GetScoreSyncInfo ();
            SyncInfo.EmailsRead += count;
        }

        public void IncrementEmailsReplied (int count=1)
        {
            EmailsReplied += count;
            GetScoreSyncInfo ();
            SyncInfo.EmailsReplied += count;
        }

        public void IncrementEmailsArchived (int count=1)
        {
            EmailsArchived += count;
            GetScoreSyncInfo ();
            SyncInfo.EmailsArchived += count;
        }

        public void IncrementEmailsDeleted (int count=1)
        {
            EmailsDeleted += count;
            GetScoreSyncInfo ();
            SyncInfo.EmailsDeleted += count;
        }

        public void UploadScore ()
        {
            Log.Debug (Log.LOG_BRAIN, "contact id = {0}", Id);
            if (null != SyncInfo) {
                // TODO - Add real implementation. Currently, just clear the delta
                ClearScoreSyncInfo ();
            }
        }

        public bool DownloadScore ()
        {
            Log.Debug (Log.LOG_BRAIN, "contact id = {0}", Id);
            return false;
        }
    }

    public class McContactScoreSyncInfo : McObject 
    {
        // Id of the corresponding McContact
        [Indexed]
        public Int64 ContactId { get; set; }

        // Number of emails receivied
        public int EmailsReceived { get; set; }

        // Number of emails read
        public int EmailsRead { get; set; }

        // Number of emails replied
        public int EmailsReplied { get; set; }

        // Number of emails archived
        public int EmailsArchived { get; set; }

        // Number of emails sent to this contact
        public int EmailsSent { get; set; }

        // Number of emails deleted without being read
        public int EmailsDeleted { get; set; }

        // If true, the score is 1.0
        public bool IsVip { get; set; }

        public McContactScoreSyncInfo ()
        {
            ContactId = 0;
            EmailsReceived = 0;
            EmailsRead = 0;
            EmailsReplied = 0;
            EmailsArchived = 0;
            EmailsSent = 0;
            EmailsDeleted = 0;
            IsVip = false;
        }
    }
}

