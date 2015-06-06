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
    public partial class McEmailAddress : McAbstrObjectPerAcc, IScorable<McEmailAddress>
    {
        // Score version of this object
        [Indexed]
        public int ScoreVersion { get; set; }

        private AnalysisFunctionsTable _AnalysisFunctions;

        [Ignore]
        public AnalysisFunctionsTable AnalysisFunctions {
            get {
                if (null == _AnalysisFunctions) {
                    _AnalysisFunctions = new AnalysisFunctionsTable () {
                    };
                }
                return _AnalysisFunctions;
            }
            set {
                _AnalysisFunctions = value;
            }
        }

        /// Time variance state machine type
        public int TimeVarianceType { get; set; }

        /// Time variance state machine current state
        public int TimeVarianceState { get; set; }

        [Indexed]
        public double Score { get; set; }

        [Indexed]
        public int NeedUpdate { get; set; }

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

        public bool ShouldUpdate ()
        {
            return (0 < NeedUpdate);
        }

        public double Classify ()
        {
            int total = EmailsReceived + EmailsSent + EmailsDeleted;
            if (0 == total) {
                return 0.0;
            }
            return (double)(EmailsRead + EmailsReplied + EmailsSent) / (double)total;
        }

        public void Analyze ()
        {
            ScoreVersion = Scoring.ApplyAnalysisFunctions (AnalysisFunctions, ScoreVersion);
            Score = Classify ();
            UpdateByBrain ();
        }

        public void MarkDependencies (NcEmailAddress.Kind addressType, int delta = 1)
        {
            switch (addressType) {
            case NcEmailAddress.Kind.From:
            case NcEmailAddress.Kind.To:
            case NcEmailAddress.Kind.Cc:
            case NcEmailAddress.Kind.Bcc:
            case NcEmailAddress.Kind.Sender:
                MarkDependentEmailMessages (addressType, delta);
                break;
            default:
                break;
            }

        }

        public void IncrementEmailsReceived (int count = 1)
        {
            EmailsReceived += count;
            MarkDependencies (NcEmailAddress.Kind.From);
        }

        public void IncrementEmailsRead (int count = 1)
        {
            EmailsRead += count;
            MarkDependencies (NcEmailAddress.Kind.From);
        }

        public void IncrementEmailsReplied (int count = 1)
        {
            EmailsReplied += count;
            MarkDependencies (NcEmailAddress.Kind.From);
        }

        public void IncrementEmailsArchived (int count = 1)
        {
            EmailsArchived += count;
            MarkDependencies (NcEmailAddress.Kind.From);
        }

        public void IncrementEmailsDeleted (int count = 1)
        {
            EmailsDeleted += count;
            MarkDependencies (NcEmailAddress.Kind.From);
        }

        public void MarkDependentEmailMessages (NcEmailAddress.Kind addressKind, int delta)
        {
            if (1 > delta) {
                delta = 1;
            }
            var queryString = String.Format (
                                  "UPDATE McEmailMessage SET NeedUpdate = NeedUpdate + {0} WHERE Id IN " +
                                  " (SELECT m.ObjectId FROM McMapEmailAddressEntry AS m " +
                                  "  WHERE m.EmailAddressId = ? AND m.AddressType = ?)", delta, (int)addressKind);
            NcModel.Instance.RunInLock (() => {
                NcModel.Instance.Db.Execute (queryString, Id);
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
                .Where (x => x.NeedUpdate > 0 && x.ScoreVersion == Scoring.Version)
                .FirstOrDefault ();
        }

        public static List<McEmailAddress> QueryNeedAnalysis (int count, int version = Scoring.Version)
        {
            return NcModel.Instance.Db.Query<McEmailAddress> (
                "SELECT a.* FROM McEmailAddress AS a " +
                " WHERE a.ScoreVersion < ? " +
                " LIMIT ?", version, count);
        }
    }
}

