//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using MimeKit;
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

        private AnalysisFunctionsTable _AnalysisFunctions;

        [Ignore]
        public AnalysisFunctionsTable AnalysisFunctions {
            get {
                if (null == _AnalysisFunctions) {
                    _AnalysisFunctions = new AnalysisFunctionsTable () {
                        { 1, AnalyzeFromAddress },
                        { 2, AnalyzeReplyStatistics },
                        // Version 3 - No statistics is updated. Just need to re-compute the score which
                        // will be done at the end of Analyze().
                        { 4, AnalyzeOtherAddresses },
                        { 5, AnalyzeSendAddresses },
                        { 6, AnalyzeHeaders },
                        { 7, AnalyzeReplies },
                        { 8, AnalyzeYahooBulkEmails },
                    };
                }
                return _AnalysisFunctions;
            }
            set {
                _AnalysisFunctions = value;
            }
        }

        protected static NcMaxScoreCombiner<McEmailMessage> QualifiersCombiner = new NcMaxScoreCombiner<McEmailMessage> ();
        protected static NcMinScoreCombiner<McEmailMessage> DisqualifiersCombiner = new NcMinScoreCombiner<McEmailMessage> ();

        public static NcVipQualifier VipQualifier = new NcVipQualifier ();
        public static NcUserActionQualifier UserActionQualifier = new NcUserActionQualifier ();
        public static NcRepliesToMyEmailsQualifier RepliesToMyEmailsQualifier = new NcRepliesToMyEmailsQualifier ();

        public static NcUserActionDisqualifier UserActionDisqualifier = new NcUserActionDisqualifier ();
        public static NcMarketingEmailDisqualifier MarketingMailDisqualifier = new NcMarketingEmailDisqualifier ();
        public static NcYahooBulkEmailDisqualifier YahooBulkEmailDisqualifier = new NcYahooBulkEmailDisqualifier ();

        /// Did the user take explicit action?
        public int UserAction { get; set; }

        [Indexed] /// Time variance state machine type
        public int TimeVarianceType { get; set; }

        /// This field is depracated and should not be used.
        public int TimeVarianceState { get; set; }

        [Indexed]
        public double Score { get; set; }

        [Indexed]
        public int NeedUpdate { get; set; }

        public const double VipScore = 1.0;

        private McEmailMessageScore DbScoreStates;

        [Ignore]
        public McEmailMessageScore ScoreStates {
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

        private static ConcurrentDictionary<int, string> _AccountAddresses = new ConcurrentDictionary<int, string> ();

        private string AccountAddress (int accountId)
        {
            string accountAddress = null;
            if (_AccountAddresses.TryGetValue (accountId, out accountAddress)) {
                return accountAddress;
            }
            // This account's address is not in the cache yet. Look it up
            var account = McAccount.QueryById<McAccount> (accountId);
            if (null == account) {
                return null;
            }
            if (!_AccountAddresses.TryAdd (accountId, account.EmailAddr)) {
                _AccountAddresses.TryGetValue (accountId, out accountAddress);
                return accountAddress;
            }
            return account.EmailAddr;
        }

        public bool ShouldUpdate ()
        {
            return (0 < NeedUpdate);
        }

        protected double BayesianLikelihood ()
        {
            double score;
            var accountAddress = AccountAddress (AccountId);
            if ((0 == ScoreVersion) && (0.0 == Score)) {
                // Version 0 quick scoring
                if (null != accountAddress) {
                    InternetAddressList addressList = NcEmailAddress.ParseAddressListString (To);
                    foreach (var addr in addressList) {
                        if (!(addr is MailboxAddress)) {
                            continue;
                        }
                        if (((MailboxAddress)addr).Address == accountAddress) {
                            return McEmailMessage.minHotScore;
                        }
                    }
                }
                // Assign a non-zero value that it is effectively 0 but it prevents
                // the same items to quanlify for quick score again.
                UpdateByBrain ((item) => {
                    var em = (McEmailMessage)item;
                    em.Score = 0.00000001;
                    return true;
                });
                return Score;
            }

            if (0 == FromEmailAddressId) {
                return 0.0;
            }
            var fromEmailAddress = McEmailAddress.QueryById<McEmailAddress> (FromEmailAddressId);
            if (null == fromEmailAddress) {
                return 0.0;
            }

            int top = 0, bottom = 0;
            fromEmailAddress.GetParts (ref top, ref bottom);

            // Incorporate the To / Cc address
            var toEmailAddresses = McEmailAddress.QueryToAddressesByMessageId (Id);
            foreach (var emailAddress in toEmailAddresses) {
                if (accountAddress == emailAddress.CanonicalEmailAddress) {
                    continue;
                }
                emailAddress.GetToParts (ref top, ref bottom);
            }
            var ccEmailAddresses = McEmailAddress.QueryCcAddressesByMessageId (Id);
            foreach (var emailAddress in ccEmailAddresses) {
                if (accountAddress == emailAddress.CanonicalEmailAddress) {
                    continue;
                }
                emailAddress.GetCcParts (ref top, ref bottom);
            }

            score = (0 == bottom) ? 0.0 : (double)top / (double)bottom;
            if (0.0 > score) {
                Log.Error (Log.LOG_BRAIN, "Invalid score {0}\n{1}", score, new StackTrace (true));
                score = 0.0;
            } else if (1.0 < score) {
                Log.Error (Log.LOG_BRAIN, "Invalid score {0}\n{1}", score, new StackTrace (true));
                score = 1.0;
            }
            NcTimeVarianceList tvList = EvaluateTimeVariance ();
            if (0 < tvList.Count) {
                DateTime now = NcTimeVariance.GetCurrentDateTime ();
                score *= tvList.Adjustment (now);
            }
            return score;
        }

        public double Classify ()
        {
            double score =
                QualifiersCombiner.Combine (this,
                    // Qualifiers are evaluated first so qualification can cause early exit and avoid excessive compute
                    VipQualifier.Classify,
                    UserActionQualifier.Classify,
                    RepliesToMyEmailsQualifier.Classify,
                    (emailMessage1) => DisqualifiersCombiner.Combine (emailMessage1,
                        // Disqualifiers are evaluated next. Again, can cause early exit and avoid excessive compute
                        UserActionDisqualifier.Classify,
                        MarketingMailDisqualifier.Classify,
                        YahooBulkEmailDisqualifier.Classify,
                        // The probablistic score is computed last because it is the most expensive.
                        (emailMessage2) => emailMessage2.BayesianLikelihood ()
                        // TODO - incorporate content score
                    )
                );
            Log.Debug (Log.LOG_BRAIN, "[McEmailMessage:{0}]: score = {1:F6}", Id, score);
            return score;
        }

        public void AnalyzeFromAddress ()
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
                    emailAddress.Score = emailAddress.Classify ();
                    NcModel.Instance.RunInTransaction (() => {
                        emailAddress.ScoreStates.Update ();
                        emailAddress.UpdateByBrain ();
                    });
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

        public void AnalyzeReplyStatistics ()
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
                        emailAddress.Score = emailAddress.Classify ();
                        NcModel.Instance.RunInTransaction (() => {
                            emailAddress.ScoreStates.Update ();
                            emailAddress.UpdateByBrain ();
                        });
                    }

                    // Initialize new columns
                    if (SetScoreIsRead (IsRead) ||
                        SetScoreIsReplied (IsReplied ())) {
                        ScoreStates.Update ();
                    }
                } else {
                    Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] Unknown email address {1}", Id, From);
                }
            } else {
                Log.Warn (Log.LOG_BRAIN, "[McEmailMessage:{0}] no valid From address ({1})", Id, From);
            }
            ScoreVersion++;
        }

        public void AnalyzeOtherAddresses ()
        {
            bool fromUpdated = false, toUpdated = false, ccUpdated = false;
            if (!String.IsNullOrEmpty (From) && (0 == FromEmailAddressId)) {
                FromEmailAddressId = McEmailAddress.Get (AccountId, From);
                if (0 < FromEmailAddressId) {
                    fromUpdated = true;
                }
            }
            if (!String.IsNullOrEmpty (To)) {
                ToEmailAddressId = McEmailAddress.GetList (AccountId, To);
                if (0 < ToEmailAddressId.Count) {
                    toUpdated = true;
                }
            }
            if (!String.IsNullOrEmpty (Cc)) {
                CcEmailAddressId = McEmailAddress.GetList (AccountId, Cc);
                if (0 < CcEmailAddressId.Count) {
                    ccUpdated = true;
                }
            }

            // Insert the address maps for to and cc address lists before we update the to / cc statistics
            // so that MarkDependencies() can correctly update emails NeedUpdate flag.
            if (fromUpdated) {
                var map = CreateAddressMap ();
                map.EmailAddressId = FromEmailAddressId;
                map.AddressType = NcEmailAddress.Kind.From;
                map.Insert ();
            }
            if (toUpdated) {
                InsertAddressList (ToEmailAddressId, NcEmailAddress.Kind.To);
            }
            if (ccUpdated) {
                InsertAddressList (CcEmailAddressId, NcEmailAddress.Kind.Cc);
            }

            // Update statistics for email addresses
            foreach (var emailAddress in McEmailAddress.QueryToAddressesByMessageId (Id)) {
                emailAddress.IncrementToEmailsReceived (markDependencies: false);
                if (IsReplied ()) {
                    emailAddress.IncrementToEmailsReplied (markDependencies: false);
                } else if (IsRead) {
                    emailAddress.IncrementToEmailsRead (markDependencies: false);
                }
                emailAddress.MarkDependencies (NcEmailAddress.Kind.To);
                emailAddress.ScoreStates.Update ();
                emailAddress.UpdateByBrain ();
            }
            foreach (var emailAddress in McEmailAddress.QueryCcAddressesByMessageId (Id)) {
                emailAddress.IncrementCcEmailsReceived (markDependencies: false);
                if (IsReplied ()) {
                    emailAddress.IncrementCcEmailsReplied (markDependencies: false);
                } else if (IsRead) {
                    emailAddress.IncrementCcEmailsRead (markDependencies: false);
                }
                emailAddress.MarkDependencies (NcEmailAddress.Kind.Cc);
                emailAddress.ScoreStates.Update ();
                emailAddress.UpdateByBrain ();
            }
        }

        public void AnalyzeSendAddresses ()
        {
            if (!IsFromMe ()) {
                return;
            }
            var otherAddresses = McEmailAddress.QueryToCcAddressByMessageId (Id);
            NcModel.Instance.RunInTransaction (() => {
                foreach (var emailAddress in otherAddresses) {
                    emailAddress.IncrementEmailsSent ();
                    emailAddress.ScoreStates.Update ();
                }
            });
        }

        public void AnalyzeHeaders ()
        {
            MarketingMailDisqualifier.Analyze (this);
        }

        protected void AnalyzeReplies ()
        {
            RepliesToMyEmailsQualifier.Analyze (this);
        }

        protected void AnalyzeYahooBulkEmails ()
        {
            YahooBulkEmailDisqualifier.Analyze (this);
        }

        public void Analyze ()
        {
            var newScoreVersion = Scoring.ApplyAnalysisFunctions (AnalysisFunctions, ScoreVersion);
            if (NcTask.Cts.Token.IsCancellationRequested) {
                UpdateByBrain ((item) => {
                    var em = (McEmailMessage)item;
                    em.ScoreVersion = newScoreVersion;
                    // If we scoring the last version, need to mark for update to recompute the score later
                    em.NeedUpdate = (Scoring.Version == newScoreVersion ? 1 : 0);
                    return true;
                });
                return;
            }
            InitializeTimeVariance ();
            var newScore = Classify ();
            var newTYpe = TimeVarianceType;
            UpdateByBrain ((item) => {
                var em = (McEmailMessage)item;
                em.Score = newScore;
                em.ScoreVersion = newScoreVersion;
                em.NeedUpdate = 0;
                em.TimeVarianceType = newTYpe;
                return true;
            });
        }

        public void IncrementTimesRead (int count = 1)
        {
            ScoreStates.TimesRead += count;
        }

        public void IncrementSecondsRead (int seconds)
        {
            ScoreStates.SecondsRead += seconds;
        }

        public bool SetScoreIsRead (bool value)
        {
            if (value == ScoreStates.IsRead) {
                return false;
            }
            ScoreStates.IsRead = value;
            return true;
        }

        public bool SetScoreIsReplied (bool value)
        {
            if (value == ScoreStates.IsReplied) {
                return false;
            }
            ScoreStates.IsReplied = value;
            return true;
        }

        public string TimeVarianceDescription ()
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

        public static List<McEmailMessage> QueryNeedUpdate (int count, bool above, int threshold = 20)
        {
            string query;
            if (above) {
                query = String.Format (
                    "SELECT e.* FROM McEmailMessage AS e " +
                    " WHERE e.NeedUpdate > ? AND e.ScoreVersion = ? " +
                    " LIMIT ?");
            } else {
                query = String.Format (
                    "SELECT e.* FROM McEmailMessage AS e " +
                    " WHERE e.NeedUpdate <= ? AND e.NeedUpdate > 0 AND e.ScoreVersion = ? " +
                    " LIMIT ?");
            }
            return NcModel.Instance.Db.Query<McEmailMessage> (query, threshold, Scoring.Version, count);
        }

        public static List<object> QueryNeedUpdateObjectsAbove (int count)
        {
            return new List<object> (QueryNeedUpdate (count, above: true));
        }

        public static List<object> QueryNeedUpdateObjectsBelow (int count)
        {
            return new List<object> (QueryNeedUpdate (count, above: false));
        }

        public static List<McEmailMessage> QueryNeedAnalysis (int count, int version = Scoring.Version)
        {
            return NcModel.Instance.Db.Query<McEmailMessage> (
                "SELECT e.* FROM McEmailMessage AS e " +
                " WHERE e.ScoreVersion < ? AND e.HasBeenGleaned > 0 " +
                " ORDER BY Id DESC " +
                " LIMIT ?", version, count);
        }

        public static List<object> QueryNeedAnalysisObjects (int count)
        {
            return new List<object> (QueryNeedAnalysis (count));
        }

        public static List<McEmailMessage> QueryNeedGleaning (Int64 accountId, int count)
        {
            var query = "SELECT e.* FROM McEmailMessage AS e " +
                        " JOIN McMapFolderFolderEntry AS m ON e.Id = m.FolderEntryId " +
                        " WHERE likelihood (HasBeenGleaned < ?, 0.1) ";
            var sqlSet = McFolder.GleaningExemptedFolderListSqlString (); 
            if (null != sqlSet) {
                query += String.Format (" AND likelihood (m.FolderId NOT IN {0}, 0.9) ", sqlSet);
            }
            if (0 <= accountId) {
                query += " AND likelihood (e.AccountId = ?, 1.0) LIMIT ?";
                return NcModel.Instance.Db.Query<McEmailMessage> (query, GleanPhaseEnum.GLEAN_PHASE2, accountId, count);
            } else {
                query += " LIMIT ?";
                return NcModel.Instance.Db.Query<McEmailMessage> (query, GleanPhaseEnum.GLEAN_PHASE2, count);
            }
        }

        public static List<McEmailMessage> QueryNeedQuickScoring (int accountId, int count)
        {
            return NcModel.Instance.Db.Query<McEmailMessage> (
                "SELECT e.* FROM McEmailMessage AS e " +
                " WHERE e.ScoreVersion = 0 AND e.Score = 0 AND e.AccountId = ? " +
                " LIMIT ?", accountId, count);
        }

        public static int CountByVersion (int version)
        {
            return NcModel.Instance.Db.Table<McEmailMessage> ().Where (x => x.ScoreVersion == version).Count ();
        }

        public static int Count ()
        {
            return NcModel.Instance.Db.Table<McEmailMessage> ().Count ();
        }

        protected T CreateTimeVariance<T> (DateTime time)
        {
            return (T)Activator.CreateInstance (typeof(T), TimeVarianceDescription (),
                (NcTimeVariance.NcTimeVarianceCallBack)TimeVarianceCallBack, Id, time);
        }

        /// <summary>
        /// Evaluate the parameters in McEmailMessage and produce a list of 
        /// NcTimeVariance that applies. These NcTimeVariance do not need to be 
        /// running. They just need to exist at point given the email parameters.
        /// </summary>
        /// <returns>List of NcTimeVariance objects</returns>
        private NcTimeVarianceList EvaluateTimeVariance ()
        {
            NcTimeVarianceList tvList = new NcTimeVarianceList ();
            DateTime deadline = DateTime.MinValue;
            DateTime deferredUntil = DateTime.MinValue;

            if (IsMeetingInvite (out deadline)) {
                // Meeting invite is a special case. If the email message is a
                // meeting invite with a valid stop time, a single deadline time
                // variance state machine is created. No other consideration is
                // needed.
                tvList.Add (CreateTimeVariance<NcMeetingTimeVariance> (deadline));
                return tvList;
            }

            ExtractDateTimeFromPair (FlagStartDate, FlagUtcStartDate, ref deferredUntil);
            ExtractDateTimeFromPair (FlagDue, FlagUtcDue, ref deadline);

            // Handle deadline (with optional defer)
            if (IsValidDateTime (deadline)) {
                tvList.Add (CreateTimeVariance<NcDeadlineTimeVariance> (deadline));

                if (IsValidDateTime (deferredUntil) && (deferredUntil < deadline)) {
                    // Make sure that deferred date is earlier than deadline. Deferring after a deadline makes
                    // no sense but the user can configure such flags. In that case, just ignore the defer
                    tvList.Add (CreateTimeVariance<NcDeferenceTimeVariance> (deferredUntil));
                }
                return tvList;
            }

            // Handle defer (no deadline)
            if (IsValidDateTime (deferredUntil)) {
                var deferTv = CreateTimeVariance<NcDeferenceTimeVariance> (deferredUntil);
                tvList.Add (deferTv);
                tvList.Add (CreateTimeVariance<NcAgingTimeVariance> (deferTv.LastEventTime ()));
                return tvList;
            }

            // Default agining only
            tvList.Add (CreateTimeVariance<NcAgingTimeVariance> (DateReceived));

            return tvList;
        }

        /// <summary>
        /// Update the time variance state in memory. Note that the caller is responsible
        /// for calling Update() if this method returns true.
        /// </summary>
        /// <returns><c>true</c>, if time variance was updated, <c>false</c> otherwise.</returns>
        /// <param name="tvList">A list of active time variance.</param>
        /// <param name="now">A timestamp to be used for finding next state for all tv.</param>
        private bool UpdateTimeVarianceStates (NcTimeVarianceList tvList, DateTime now)
        {
            var newType = tvList.LastTimeVarianceType (now);
            if (TimeVarianceType == (int)newType) {
                return false;
            }
            TimeVarianceType = (int)newType;
            return true;
        }

        private void InitializeTimeVariance ()
        {
            Log.Debug (Log.LOG_BRAIN, "Initialize time variance for email message id {0}", Id);

            if (0 == ScoreVersion) {
                return;
            }

            DateTime now = NcTimeVariance.GetCurrentDateTime ();
            NcTimeVarianceList tvList = EvaluateTimeVariance ();

            /// Start all applicable state machines
            if (!tvList.Start (now)) {
                // All TV SMs finish for this email message. Compute the final score
                Score = Classify ();
            }

            if (UpdateTimeVarianceStates (tvList, now)) {
                var newScore = Score;
                var newType = TimeVarianceType;
                UpdateByBrain ((item) => {
                    var em = (McEmailMessage)item;
                    em.Score = newScore;
                    em.TimeVarianceType = newType;
                    return true;
                });
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

        public void UpdateByBrain (Mutator change)
        {
            int rc;
            UpdateWithOCApply<McEmailMessage> (change, out rc);
            NcBrain brain = NcBrain.SharedInstance;
            brain.McEmailMessageCounters.Update.Click ();
            brain.NotifyEmailMessageUpdates ();
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
            if (0 < Id) {
                UpdateWithOCApply<McEmailMessage> ((item) => {
                    var em = (McEmailMessage)item;
                    em.HasBeenGleaned = (int)phase;
                    return true;
                });
            }
        }

        private static void TimeVarianceCallBack (int state, Int64 objId)
        {
            McEmailMessage emailMessage = McEmailMessage.QueryById<McEmailMessage> ((int)objId);
            if (null == emailMessage) {
                return; // The object has been deleted
            }

            // Update time variance state if necessary
            DateTime now = NcTimeVariance.GetCurrentDateTime ();
            NcTimeVarianceList tvList = emailMessage.EvaluateTimeVariance ();
            bool fullUpdateNeeded = emailMessage.UpdateTimeVarianceStates (tvList, now);

            // Recompute a new score and update it in the cache
            bool scoreChanged = false;
            double newScore = emailMessage.Classify ();
            if (newScore != emailMessage.Score) {
                emailMessage.Score = newScore;
                scoreChanged = true;
            }
            if (fullUpdateNeeded || scoreChanged) {
                emailMessage.NeedUpdate = 0;
                if (fullUpdateNeeded) {
                    var newType = emailMessage.TimeVarianceType;
                    emailMessage.UpdateByBrain ((item) => {
                        var em = (McEmailMessage)item;
                        em.Score = newScore;
                        em.TimeVarianceType = newType;
                        return true;
                    });
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
                if (token.IsCancellationRequested) {
                    return;
                }
                var emailMessage = McEmailMessage.QueryById<McEmailMessage> (emailMessageId.Id);
                if (null == emailMessage) {
                    continue;
                }
                emailMessage.UpdateTimeVariance ();
                numStarted++;

                /// Throttle
                n = (n + 1) % 8;
                if (0 == n) {
                    if (!NcTask.CancelableSleep (100, token)) {
                        break;
                    }
                }
            }
            Log.Info (Log.LOG_BRAIN, "{0} time variances started", numStarted);
        }

        protected void InsertScoreStates ()
        {
            NcAssert.True ((0 < AccountId) && (0 < Id));
            DbScoreStates = new McEmailMessageScore () {
                AccountId = AccountId,
                ParentId = Id,
            };
            DbScoreStates.Insert ();
        }

        protected void ReadScoreStates ()
        {
            DbScoreStates = McEmailMessageScore.QueryByParentId (Id);
            if (null == DbScoreStates) {
                Log.Error (Log.LOG_BRAIN, "fail to get score states for email message {0}. create one", Id);
                InsertScoreStates ();
            }
        }

        protected void DeleteScoreStates ()
        {
            DbScoreStates = null;
            McEmailMessageScore.DeleteByParentId (Id);
        }

        public bool IsFromMe ()
        {
            if (String.IsNullOrEmpty (From)) {
                return false;
            }
            MailboxAddress mbAddr = NcEmailAddress.ParseMailboxAddressString (From);
            var accountAddress = AccountAddress (AccountId);
            if (null == accountAddress) {
                return false;
            }
            if (null == mbAddr) {
                return false;
            }
            return accountAddress == mbAddr.Address;
        }

        protected void UpdateAnalysisInternal (DateTime newTime, double variance, Func<DateTime, double, bool> updateFunc, Func<bool, bool> setFunc, 
                                               Action<McEmailAddress, int> fromFunc, Action<McEmailAddress, int> toFunc, Action<McEmailAddress, int> ccFunc)
        {
            NcModel.Instance.RunInTransaction (() => {
                if (updateFunc (newTime, variance)) {
                    int delta = DateTime.MinValue == newTime ? -1 : +1;
                    if (2 <= ScoreVersion) {
                        if (setFunc (DateTime.MinValue != newTime)) {
                            ScoreStates.Update ();
                        } else {
                            delta = 0;
                        }
                        if (0 != delta) {
                            var emailAddress = McEmailAddress.QueryById<McEmailAddress> (FromEmailAddressId);
                            if (null != emailAddress) {
                                fromFunc (emailAddress, delta);
                                emailAddress.ScoreStates.Update ();
                            }
                        }
                    }
                    if ((4 <= ScoreVersion) && (0 != delta)) {
                        var accountAddress = AccountAddress (AccountId);
                        foreach (var emailAddress in McEmailAddress.QueryToAddressesByMessageId (Id)) {
                            if (accountAddress == emailAddress.CanonicalEmailAddress) {
                                continue;
                            }
                            toFunc (emailAddress, delta);
                            emailAddress.ScoreStates.Update ();
                        }
                        foreach (var emailAddress in McEmailAddress.QueryCcAddressesByMessageId (Id)) {
                            if (accountAddress == emailAddress.CanonicalEmailAddress) {
                                continue;
                            }
                            ccFunc (emailAddress, delta);
                            emailAddress.ScoreStates.Update ();
                        }
                    }
                    if (Scoring.Version == ScoreVersion) {
                        Score = Classify ();
                        NeedUpdate = 0;
                        UpdateScoreAndNeedUpdate ();
                    }
                }
            });
        }

        public void UpdateReadAnalysis (DateTime readTime, double variance)
        {
            UpdateAnalysisInternal (readTime, variance, ScoreStates.UpdateReadTime, SetScoreIsRead,
                (emailAdddress, delta) => {
                    emailAdddress.IncrementEmailsRead (delta);
                },
                (emailAddress, delta) => {
                    emailAddress.IncrementToEmailsRead (delta);
                },
                (emailAddress, delta) => {
                    emailAddress.IncrementCcEmailsRead (delta);
                }
            );
        }

        public void UpdateReplyAnalysis (DateTime replyTime, double variance)
        {
            UpdateAnalysisInternal (replyTime, variance, ScoreStates.UpdateReplyTime, SetScoreIsReplied,
                (emailAddress, delta) => {
                    emailAddress.IncrementEmailsReplied (delta);
                },
                (emailAddress, delta) => {
                    emailAddress.IncrementToEmailsReplied (delta);
                },
                (emailAddress, delta) => {
                    emailAddress.IncrementCcEmailsReplied (delta);
                }
            );
        }
    }
}
