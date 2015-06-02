//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Utils;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public partial class McEmailAddress : McAbstrObjectPerAcc, IScorable
    {
        // Score version of this object
        [Indexed]
        public int ScoreVersion { get; set; }

        /// Time variance state machine type
        public int TimeVarianceType { get; set; }

        /// Time variance state machine current state
        public int TimeVarianceState { get; set; }

        [Indexed]
        public double Score { get; set; }

        [Indexed]
        public bool NeedUpdate { get; set; }

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

        public double GetScore ()
        {
            int total = EmailsReceived + EmailsSent + EmailsDeleted;
            if (0 == total) {
                return 0.0;
            }
            return (double)(EmailsRead + EmailsReplied + EmailsSent) / (double)total;
        }

        public void ScoreObject ()
        {
            if (0 == ScoreVersion) {
                ScoreVersion++;
            }
            if (1 == ScoreVersion) {
                // Version 2 statistics are updated by emails. Nothing to do here
                ScoreVersion++;
            }
            if (2 == ScoreVersion) {
                // No statisitcs are updated in v3. Just need to recompute the score
                // using the new function.
                ScoreVersion++;
            }
            NcAssert.True (Scoring.Version == ScoreVersion);
            Score = GetScore ();
            UpdateByBrain ();
        }

        public void MarkDependencies ()
        {
            MarkDependentEmailMessages ();
            // TODO - mark dependent meetings later
        }

        public void IncrementEmailsReceived (int count = 1)
        {
            EmailsReceived += count;
            MarkDependencies ();
        }

        public void IncrementEmailsRead (int count = 1)
        {
            EmailsRead += count;
            MarkDependencies ();
        }

        public void IncrementEmailsReplied (int count = 1)
        {
            EmailsReplied += count;
            MarkDependencies ();
        }

        public void IncrementEmailsArchived (int count = 1)
        {
            EmailsArchived += count;
            MarkDependencies ();
        }

        public void IncrementEmailsDeleted (int count = 1)
        {
            EmailsDeleted += count;
            MarkDependencies ();
        }

        public void MarkDependentEmailMessages ()
        {
            NcModel.Instance.RunInLock (() => {
                NcModel.Instance.Db.Execute (
                    "UPDATE McEmailMessage SET NeedUpdate = 1 WHERE " +
                    " Id IN (SELECT EmailMessageId FROM McEmailMessageDependency AS d WHERE d.EmailAddressId = ?)", Id);
            });
        }

        public void InsertByBrain ()
        {
            int rc = Insert ();
            if (0 < rc) {
                NcBrain brain = NcBrain.SharedInstance;
                brain.McEmailAddressCounters.Insert.Click ();
                brain.NotifyEmailAddressUpdates ();
            }
        }

        public void UpdateByBrain ()
        {
            int rc = Update ();
            if (0 < rc) {
                NcBrain brain = NcBrain.SharedInstance;
                brain.McEmailAddressCounters.Update.Click ();
                brain.NotifyEmailAddressUpdates ();
            }
        }

        public void DeleteByBrain ()
        {
            int rc = Delete ();
            if (0 < rc) {
                NcBrain brain = NcBrain.SharedInstance;
                brain.McEmailAddressCounters.Delete.Click ();
                brain.NotifyEmailAddressUpdates ();
            }
        }

        public static McEmailAddress QueryNeedUpdate ()
        {
            return NcModel.Instance.Db.Table<McEmailAddress> ()
                .Where (x => x.NeedUpdate && x.ScoreVersion == Scoring.Version)
                .FirstOrDefault ();
        }

        public static McEmailAddress QueryNeedAnalysis ()
        {
            return NcModel.Instance.Db.Table<McEmailAddress> ()
                .Where (x => x.ScoreVersion < Scoring.Version)
                .FirstOrDefault ();
        }
    }
}

