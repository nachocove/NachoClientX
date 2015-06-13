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
        //////////////////// IScorable members ////////////////////
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
        public int NeedUpdate { get; set; }

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

        // Synced score states
        private McEmailAddressScore DbScoreStates;

        [Ignore]
        public McEmailAddressScore ScoreStates {
            get {
                if (null == DbScoreStates) {
                    ReadScoreStates ();
                }
                return DbScoreStates;
            }
            set {
                DbScoreStates = value;
            }
        }

        public bool ShouldUpdate ()
        {
            return (0 < NeedUpdate);
        }

        public double Classify ()
        {
            int total = ScoreStates.EmailsReceived + ScoreStates.EmailsSent + ScoreStates.EmailsDeleted;
            if (0 == total) {
                return 0.0;
            }
            return (double)(ScoreStates.EmailsRead + ScoreStates.EmailsReplied + ScoreStates.EmailsSent) / (double)total;
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

        ///////////////////// From address methods /////////////////////
        public void IncrementEmailsReceived (int count = 1)
        {
            ScoreStates.EmailsReceived += count;
            MarkDependencies (NcEmailAddress.Kind.From);
        }

        public void IncrementEmailsRead (int count = 1)
        {
            ScoreStates.EmailsRead += count;
            MarkDependencies (NcEmailAddress.Kind.From);
        }

        public void IncrementEmailsReplied (int count = 1)
        {
            ScoreStates.EmailsReplied += count;
            MarkDependencies (NcEmailAddress.Kind.From);
        }

        public void IncrementEmailsArchived (int count = 1)
        {
            ScoreStates.EmailsArchived += count;
            MarkDependencies (NcEmailAddress.Kind.From);
        }

        public void IncrementEmailsDeleted (int count = 1)
        {
            ScoreStates.EmailsDeleted += count;
            MarkDependencies (NcEmailAddress.Kind.From);
        }

        ///////////////////// To address methods /////////////////////
        public void IncrementToEmailsReceived (int count = 1, bool markDependencies = true)
        {
            ScoreStates.ToEmailsReceived += count;
            if (markDependencies) {
                MarkDependencies (NcEmailAddress.Kind.To);
            }
        }

        public void IncrementToEmailsRead (int count = 1, bool markDependencies = true)
        {
            ScoreStates.ToEmailsRead += count;
            if (markDependencies) {
                MarkDependencies (NcEmailAddress.Kind.To);
            }
        }

        public void IncrementToEmailsReplied (int count = 1, bool markDependencies = true)
        {
            ScoreStates.ToEmailsReplied += count;
            if (markDependencies) {
                MarkDependencies (NcEmailAddress.Kind.To);
            }
        }

        public void IncrementToEmailsArchived (int count = 1, bool markDependencies = true)
        {
            ScoreStates.ToEmailsArchived += count;
            if (markDependencies) {
                MarkDependencies (NcEmailAddress.Kind.To);
            }
        }

        public void IncrementToEmailsDeleted (int count = 1, bool markDependencies = true)
        {
            ScoreStates.ToEmailsDeleted += count;
            if (markDependencies) {
                MarkDependencies (NcEmailAddress.Kind.To);
            }
        }

        ///////////////////// Cc address methods /////////////////////
        public void IncrementCcEmailsReceived (int count = 1, bool markDependencies = true)
        {
            ScoreStates.CcEmailsReceived += count;
            if (markDependencies) {
                MarkDependencies (NcEmailAddress.Kind.Cc);
            }
        }

        public void IncrementCcEmailsRead (int count = 1, bool markDependencies = true)
        {
            ScoreStates.CcEmailsRead += count;
            if (markDependencies) {
                MarkDependencies (NcEmailAddress.Kind.Cc);
            }
        }

        public void IncrementCcEmailsReplied (int count = 1, bool markDependencies = true)
        {
            ScoreStates.CcEmailsReplied += count;
            if (markDependencies) {
                MarkDependencies (NcEmailAddress.Kind.Cc);
            }
        }

        public void IncrementCcEmailsArchived (int count = 1, bool markDependencies = true)
        {
            ScoreStates.CcEmailsArchived += count;
            if (markDependencies) {
                MarkDependencies (NcEmailAddress.Kind.Cc);
            }
        }

        public void IncrementCcEmailsDeleted (int count = 1, bool markDependencies = true)
        {
            ScoreStates.CcEmailsDeleted += count;
            if (markDependencies) {
                MarkDependencies (NcEmailAddress.Kind.Cc);
            }
        }

        public void MarkDependentEmailMessages (NcEmailAddress.Kind addressKind, int delta)
        {
            if (1 > delta) {
                delta = 1;
            }
            var queryString = String.Format (
                                  "UPDATE McEmailMessage SET NeedUpdate = NeedUpdate + {0} WHERE Id IN " +
                                  " (SELECT m.ObjectId FROM McMapEmailAddressEntry AS m " +
                                  "  WHERE m.EmailAddressId = ? AND m.AddressType = ?)", delta);
            NcModel.Instance.RunInLock (() => {
                NcModel.Instance.Db.Execute (queryString, Id, (int)addressKind);
            });
        }

        public override int Insert ()
        {
            int rc = 0;
            NcModel.Instance.RunInTransaction (() => {
                rc = base.Insert ();
                InsertScoreStates ();
            });
            return rc;
        }

        public override int Delete ()
        {
            int rc = 0;
            NcModel.Instance.RunInTransaction (() => {
                rc = base.Delete ();
                DeleteScoreStates ();
            });
            return rc;
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

        protected void InsertScoreStates ()
        {
            NcAssert.True ((0 < AccountId) && (0 < Id));
            DbScoreStates = new McEmailAddressScore () {
                AccountId = AccountId,
                ParentId = Id,
            };
            DbScoreStates.Insert ();
        }

        protected void ReadScoreStates ()
        {
            DbScoreStates = McEmailAddressScore.QueryByParentId (Id);
            if (null == DbScoreStates) {
                Log.Error (Log.LOG_BRAIN, "fail to find score states for email address {0}. Create one", Id);
                InsertScoreStates ();
            }
        }

        protected void DeleteScoreStates ()
        {
            McEmailAddressScore.DeleteByParentId (Id);
        }
    }
}

