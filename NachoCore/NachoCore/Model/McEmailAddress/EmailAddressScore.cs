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

        // We don't use Score2 yet.
        public double Score2 { get; set; }

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

        public void GetParts (ref int top, ref int bottom)
        {
            top += (
                ScoreStates.EmailsRead + ScoreStates.EmailsReplied +
                ScoreStates.EmailsSent +
                ScoreStates.MarkedHot
            );
            bottom += (
                ScoreStates.EmailsReceived +
                ScoreStates.EmailsSent +
                ScoreStates.EmailsDeleted +
                ScoreStates.MarkedHot + ScoreStates.MarkedNotHot
            );
        }

        public void GetToParts (ref int top, ref int bottom)
        {
            top += ScoreStates.ToEmailsRead + ScoreStates.ToEmailsReplied;
            bottom += ScoreStates.ToEmailsReceived + ScoreStates.ToEmailsDeleted;
        }

        public void GetCcParts (ref int top, ref int bottom)
        {
            top += ScoreStates.CcEmailsRead + ScoreStates.CcEmailsReplied;
            bottom += ScoreStates.CcEmailsReceived + ScoreStates.CcEmailsDeleted;
        }

        public Tuple<double, double> Classify ()
        {
            int top = 0, bottom = 0;
            GetParts (ref top, ref bottom);
            if (0 == bottom) {
                return new Tuple<double, double> (0.0, 0.0);
            }
            return new Tuple<double, double> ((double)top / (double)bottom, 0.0);
        }

        public void Analyze ()
        {
            ScoreVersion = Scoring.ApplyAnalysisFunctions (AnalysisFunctions, ScoreVersion);
            var newScores = Classify ();
            Score = newScores.Item1;
            Score2 = newScores.Item2;
            UpdateByBrain ();
        }

        public void MarkDependencies (EmailMessageAddressType addressType, int delta = 1)
        {

            MarkDependentEmailMessages (addressType, delta);

        }

        ///////////////////// From address methods /////////////////////
        public void IncrementEmailsReceived (int count = 1)
        {
            ScoreStates.EmailsReceived += count;
            MarkDependencies (EmailMessageAddressType.From);
        }

        public void IncrementEmailsRead (int count = 1)
        {
            ScoreStates.EmailsRead += count;
            if (ScoreStates.CheckStatistics ("IncrementEmailsRead")) {
                MarkDependencies (EmailMessageAddressType.From);
            } else {
                ScoreStates.EmailsRead -= count;
            }
        }

        public void IncrementEmailsReplied (int count = 1)
        {
            ScoreStates.EmailsReplied += count;
            if (ScoreStates.CheckStatistics ("IncrementEmailsReplied")) {
                MarkDependencies (EmailMessageAddressType.From);
            } else {
                ScoreStates.EmailsReplied -= count;
            }
        }

        public void IncrementEmailsArchived (int count = 1)
        {
            ScoreStates.EmailsArchived += count;
            MarkDependencies (EmailMessageAddressType.From);
        }

        public void IncrementEmailsDeleted (int count = 1)
        {
            ScoreStates.EmailsDeleted += count;
            MarkDependencies (EmailMessageAddressType.From);
        }

        public void IncrementEmailsSent (int count = 1)
        {
            ScoreStates.EmailsSent += count;
            MarkDependencies (EmailMessageAddressType.To);
            MarkDependencies (EmailMessageAddressType.Cc);
        }

        ///////////////////// To address methods /////////////////////
        public void IncrementToEmailsReceived (int count = 1, bool markDependencies = true)
        {
            ScoreStates.ToEmailsReceived += count;
            if (markDependencies) {
                MarkDependencies (EmailMessageAddressType.To);
            }
        }

        public void IncrementToEmailsRead (int count = 1, bool markDependencies = true)
        {
            ScoreStates.ToEmailsRead += count;
            if (ScoreStates.CheckToStatistics ("IncrementToEmailsRead")) {
                if (markDependencies) {
                    MarkDependencies (EmailMessageAddressType.To);
                }
            } else {
                ScoreStates.ToEmailsRead -= count;
            }
        }

        public void IncrementToEmailsReplied (int count = 1, bool markDependencies = true)
        {
            ScoreStates.ToEmailsReplied += count;
            if (ScoreStates.CheckToStatistics ("IncrementToEmailsReplied")) {
                if (markDependencies) {
                    MarkDependencies (EmailMessageAddressType.To);
                }
            } else {
                ScoreStates.ToEmailsReplied -= count;
            }
        }

        public void IncrementToEmailsArchived (int count = 1, bool markDependencies = true)
        {
            ScoreStates.ToEmailsArchived += count;
            if (markDependencies) {
                MarkDependencies (EmailMessageAddressType.To);
            }
        }

        public void IncrementToEmailsDeleted (int count = 1, bool markDependencies = true)
        {
            ScoreStates.ToEmailsDeleted += count;
            if (markDependencies) {
                MarkDependencies (EmailMessageAddressType.To);
            }
        }

        ///////////////////// Cc address methods /////////////////////
        public void IncrementCcEmailsReceived (int count = 1, bool markDependencies = true)
        {
            ScoreStates.CcEmailsReceived += count;
            if (markDependencies) {
                MarkDependencies (EmailMessageAddressType.Cc);
            }
        }

        public void IncrementCcEmailsRead (int count = 1, bool markDependencies = true)
        {
            ScoreStates.CcEmailsRead += count;
            if (ScoreStates.CheckCcStatistics ("IncrementCcEmailsRead")) {
                if (markDependencies) {
                    MarkDependencies (EmailMessageAddressType.Cc);
                }
            } else {
                ScoreStates.CcEmailsRead -= count;
            }
        }

        public void IncrementCcEmailsReplied (int count = 1, bool markDependencies = true)
        {
            ScoreStates.CcEmailsReplied += count;
            if (ScoreStates.CheckCcStatistics ("IncrementCcEmailsReplied")) {
                if (markDependencies) {
                    MarkDependencies (EmailMessageAddressType.Cc);
                }
            } else {
                ScoreStates.CcEmailsReplied -= count;
            }
        }

        public void IncrementCcEmailsArchived (int count = 1, bool markDependencies = true)
        {
            ScoreStates.CcEmailsArchived += count;
            if (markDependencies) {
                MarkDependencies (EmailMessageAddressType.Cc);
            }
        }

        public void IncrementCcEmailsDeleted (int count = 1, bool markDependencies = true)
        {
            ScoreStates.CcEmailsDeleted += count;
            if (markDependencies) {
                MarkDependencies (EmailMessageAddressType.Cc);
            }
        }

        public void MarkDependentEmailMessages (EmailMessageAddressType addressKind, int delta)
        {
            if (1 > delta) {
                delta = 1;
            }

            var queryString2 = String.Format (
                "UPDATE McEmailMessageNeedsUpdate SET NeedsUpdate = NeedsUpdate + {0} " +
                " WHERE " +
                " EmailMessageId IN " +
                " (SELECT m.ObjectId FROM McMapEmailAddressEntry AS m WHERE m.EmailAddressId = ? AND m.AddressType = ?)",
                delta);
            NcModel.Instance.RunInLock (() => {
                NcModel.Instance.Db.Execute (queryString2, Id, (int)addressKind);
            });

        }

        public override int Insert ()
        {
            using (var capture = CaptureWithStart ("Insert")) {
                int rc = 0;
                NcModel.Instance.RunInTransaction (() => {
                    rc = base.Insert ();
                    if (1 == rc) {
                        InsertScoreStates ();
                    } else {
                        Log.Error (Log.LOG_BRAIN, "McEmailAddress.Insert returned {0}", rc);
                    }
                });
                return rc;
            }
        }

        public override int Delete ()
        {
            using (var capture = CaptureWithStart ("Delete")) {
                int rc = 0;
                NcModel.Instance.RunInTransaction (() => {
                    rc = base.Delete ();
                    DeleteScoreStates ();
                });
                return rc;
            }
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
            NcModel.Instance.RunInTransaction (() => {
                DbScoreStates = McEmailAddressScore.QueryByParentId (Id);
                if (null == DbScoreStates) {
                    Log.Error (Log.LOG_BRAIN, "fail to find score states for email address {0}. Create one", Id);
                    InsertScoreStates ();
                }
            });
        }

        protected void DeleteScoreStates ()
        {
            McEmailAddressScore.DeleteByParentId (Id);
        }
    }
}

