//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoCore.Index;

namespace NachoCore.Utils
{

    class EmailSearchResults
    {
        public string Query;
        public string [] Tokens;
        public readonly int [] ContactIds;
        public readonly int [] MessageIds;

        public EmailSearchResults (string query, string [] tokens, int [] contactIds, int [] messageIds)
        {
            Query = query;
            Tokens = tokens;
            ContactIds = contactIds;
            MessageIds = messageIds;
        }
    }

    class EmailSearcher : Searcher<EmailSearchResults>
    {
        public McAccount Account;
        public int MaxContactResults = 5;
        public int MaxMessageResults = 100;

        protected override EmailSearchResults SearchResults (string query)
        {
            var accountId = (Account?.AccountType ?? McAccount.AccountTypeEnum.Unified) == McAccount.AccountTypeEnum.Unified ? 0 : Account.Id;
            var contactResults = NcIndex.Main.SearchContactsNameAndEmails (query, maxResults: MaxContactResults);
            var messageResults = NcIndex.Main.SearchEmails (query, accountId: accountId, maxResults: MaxMessageResults);
            var contactIds = contactResults.Documents.Select (r => r.IntegerContactId).ToArray ();
            var messageIds = messageResults.Documents.Select (r => r.IntegerMessageId).ToArray ();
            return new EmailSearchResults (query, contactResults.ParsedTokens, contactIds, messageIds);
        }
    }

    class EmailServerSearcher : ServerSearcher<int []>
    {
        public McAccount Account;

        protected override Dictionary<int, string> CreateServerSearchTokens (string query)
        {
            var tokens = new Dictionary<int, string> ();
            if (Account.AccountType == McAccount.AccountTypeEnum.Unified) {
                var accounts = McAccount.QueryByAccountCapabilities (McAccount.AccountCapabilityEnum.EmailReaderWriter).Where (x => x.AccountType != McAccount.AccountTypeEnum.Unified).ToList ();
                foreach (var account in accounts) {
                    tokens [account.Id] = BackEnd.Instance.StartSearchEmailReq (account.Id, query, null).GetValue<string> ();
                }
            } else {
                tokens [Account.Id] = BackEnd.Instance.StartSearchEmailReq (Account.Id, query, null).GetValue<string> ();
            }
            return tokens;
        }

        protected override NcResult.SubKindEnum SuccessStatus {
            get {
                return NcResult.SubKindEnum.Info_EmailSearchCommandSucceeded;
            }
        }

        protected override NcResult.SubKindEnum ErrorStatus {
            get {
                return NcResult.SubKindEnum.Error_EmailSearchCommandFailed;
            }
        }

        public override void Search (string query)
        {
            ResultsFromAllAccounts.Clear ();
            base.Search (query);
        }

        readonly List<int> ResultsFromAllAccounts = new List<int> ();

        protected override void HandleStatus (NcResult status)
        {
            var messageIds = status.GetValue<List<NcEmailMessageIndex>> ().Select (x => x.Id);
            ResultsFromAllAccounts.AddRange (messageIds);
            FoundResults (ResultsFromAllAccounts.ToArray ());
        }
    }

    public class EmailSearch : NachoEmailMessages
    {
        public delegate void UpdateUiAction (string searchString, List<McEmailMessageThread> results);

        UpdateUiAction updateUi;

        object lockObject = new object ();
        object serverResultsLock = new object ();

        bool searchInProgress = false;
        string lastSearch = "";
        string nextSearch = null;
        bool serverResultsPending = false;

        List<McAccount> accounts;
        Dictionary<int, string> accountSearchTokens;

        IEnumerable<EmailMessageDocument> indexResults;
        Dictionary<int, List<NcEmailMessageIndex>> serverResults = new Dictionary<int, List<NcEmailMessageIndex>> ();

        List<McEmailMessageThread> finalResults = new List<McEmailMessageThread> ();

        public EmailSearch (UpdateUiAction updateUi)
        {
            this.updateUi = updateUi;
        }

        public void EnterSearchMode (McAccount account)
        {
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            if (McAccount.AccountTypeEnum.Unified == account.AccountType) {
                accounts = McAccount.QueryByAccountCapabilities (McAccount.AccountCapabilityEnum.EmailReaderWriter).Where (x => x.AccountType != McAccount.AccountTypeEnum.Unified).ToList ();
            } else {
                accounts = new List<McAccount> ();
                accounts.Add (account);
            }
            lock (serverResultsLock) {
                serverResults.Clear ();
            }
            ClearCache ();
            indexResults = new EmailMessageDocument [0];
            finalResults = new List<McEmailMessageThread> ();
        }

        public void ExitSearchMode ()
        {
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            lock (lockObject) {
                nextSearch = null;
                serverResultsPending = false;
            }
            CancelServerSearches ();
        }

        public override bool IncludesMultipleAccounts ()
        {
            return accounts.Count > 1;
        }

        public void SearchFor (string searchString)
        {
            // Since the search string has changed, clear any server results and prevent
            // any pending server requests from completing.
            lock (serverResultsLock) {
                serverResults.Clear ();
            }
            CancelServerSearches ();

            if (null == searchString) {
                searchString = "";
            }

            bool startSearching;
            lock (lockObject) {
                nextSearch = searchString;
                serverResultsPending = false;
                startSearching = !searchInProgress;
                searchInProgress = true;
            }

            if (startSearching) {
                StartSearch ();
            }
        }

        public void StartServerSearch ()
        {
            CancelServerSearches ();
            string searchString = nextSearch ?? lastSearch;
            if (!string.IsNullOrEmpty (searchString)) {
                accountSearchTokens = new Dictionary<int, string> ();
                foreach (var account in accounts) {
                    accountSearchTokens [account.Id] = BackEnd.Instance.StartSearchEmailReq (account.Id, searchString, null).GetValue<string> ();
                }
            }
        }

        #region INachoEmailMessages

        public override bool BeginRefresh (out List<int> adds, out List<int> deletes)
        {
            // TODO  Should this rerun the search??

            adds = null;
            deletes = null;
            return false;
        }

        public override void CommitRefresh ()
        {
        }

        public override int Count ()
        {
            return finalResults.Count;
        }

        public override McEmailMessageThread GetEmailThread (int i)
        {
            if (i >= finalResults.Count) {
                return null;
            }
            var thread = finalResults [i];
            thread.Source = this;
            return thread;
        }

        public override List<McEmailMessageThread> GetEmailThreadMessages (int id)
        {
            var threads = new List<McEmailMessageThread> ();
            threads.Add (new McEmailMessageThread () {
                FirstMessageId = id,
                MessageCount = 1,
            });
            return threads;
        }

        public override string DisplayName ()
        {
            return "Search";
        }

        public override bool IsCompatibleWithAccount (McAccount otherAccount)
        {
            return McAccount.AccountTypeEnum.Unified == otherAccount.AccountType || accounts.Where (x => x.Id == otherAccount.Id).Any ();
        }

        #endregion

        void StartSearch ()
        {
            NcTask.Run (() => {

                bool keepSearching = false;
                do {
                    bool mergeOnly;
                    string searchString;
                    lock (lockObject) {
                        mergeOnly = serverResultsPending;
                        if (mergeOnly) {
                            searchString = lastSearch;
                            serverResultsPending = false;
                        } else {
                            lastSearch = nextSearch;
                            searchString = nextSearch;
                            nextSearch = null;
                        }
                    }

                    if (!mergeOnly) {
                        if (!string.IsNullOrEmpty (searchString)) {
                            int maxResults = Math.Min (100 * searchString.Length, 1000);
                            var accountId = accounts.Count == 1 ? accounts [0].Id : 0;
                            indexResults = NcIndex.Main.SearchEmails (searchString, accountId).Documents;
                        } else {
                            indexResults = new EmailMessageDocument [0];
                        }
                    }

                    // Merge the index results and server results, avoiding duplicates and bad items.
                    // Score the matches, and sort from highest to lowest score.
                    var searchWords = searchString.Trim ().Split (new char [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var dbIds = new HashSet<int> ();
                    var messageIds = new HashSet<string> ();
                    var scoredResults = new List<SearchMatch> ();

                    NcAssert.NotNull (indexResults, "StartSearch: indexResults is null");
                    foreach (var indexMatch in indexResults) {
                        NcAssert.NotNull (indexMatch, "StartSearch: indexMatch is null");
                        float matchScore;
                        int id = indexMatch.IntegerMessageId;
                        if (ScoreMessage (id, searchWords, indexMatch.Score, out matchScore, dbIds, messageIds)) {
                            scoredResults.Add (new SearchMatch () {
                                thread = new McEmailMessageThread () {
                                    FirstMessageId = id,
                                    MessageCount = 1,
                                },
                                matchScore = matchScore,
                            });
                        }
                    }
                    // The serverResults collection can be modified by StatusIndicatorCallback at any time.
                    // So access is protected by a lock.  Make a copy of the values rather than holding the
                    // lock while iterating over the collection so that the UI thread is not blocked for an
                    // extended period of time.
                    List<List<NcEmailMessageIndex>> allAccountsServerResults;
                    lock (serverResultsLock) {
                        var values = serverResults.Values;
                        NcAssert.NotNull (values, "StartSearch: serverResults.Values is null");
                        allAccountsServerResults = new List<List<NcEmailMessageIndex>> (values);
                    }
                    NcAssert.NotNull (allAccountsServerResults, "StartSearch: allAccountsServerResults is null");
                    foreach (var serverMatches in allAccountsServerResults) {
                        foreach (var serverMatch in serverMatches) {
                            NcAssert.NotNull (serverMatch, "StartSearch: serverMatch is null");
                            float matchScore;
                            if (ScoreMessage (serverMatch.Id, searchWords, 0, out matchScore, dbIds, messageIds)) {
                                scoredResults.Add (new SearchMatch () {
                                    thread = new McEmailMessageThread () {
                                        FirstMessageId = serverMatch.Id,
                                        MessageCount = 1,
                                    },
                                    matchScore = matchScore,
                                });
                            }
                        }
                    }
                    scoredResults.Sort ((SearchMatch x, SearchMatch y) => {
                        return Comparer<float>.Default.Compare (y.matchScore, x.matchScore);
                    });
                    if (100 < scoredResults.Count) {
                        scoredResults.RemoveRange (100, scoredResults.Count - 100);
                    }

                    var results = new List<McEmailMessageThread> (scoredResults.Select (x => x.thread));

                    NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                        ClearCache ();
                        finalResults = results;
                        updateUi (searchString, results);
                    });

                    lock (lockObject) {
                        keepSearching = (null != nextSearch) || serverResultsPending;
                        if (!keepSearching) {
                            searchInProgress = false;
                        }
                    }
                } while (keepSearching);
            }, "EmailSearch");
        }

        bool ScoreMessage (int messageId, string [] searchWords, float indexScore, out float score, HashSet<int> dbIds, HashSet<string> messageIds)
        {
            score = 0;
            if (!dbIds.Add (messageId)) {
                return false;
            }
            var message = McEmailMessage.QueryById<McEmailMessage> (messageId);
            if (null == message || message.IsDeferred () || (!string.IsNullOrEmpty (message.MessageID) && !messageIds.Add (message.MessageID))) {
                return false;
            }

            int localScore = 0;
            score = (float)localScore + indexScore;
            return true;
        }

        void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_EmailSearchCommandSucceeded == s.Status.SubKind &&
                null != s.Account && null != s.Tokens && null != accountSearchTokens &&
                accountSearchTokens.Keys.Contains (s.Account.Id) &&
                s.Tokens.Contains (accountSearchTokens [s.Account.Id])) {
                var matches = s.Status.GetValue<List<NcEmailMessageIndex>> ();
                if (0 < matches.Count || serverResults.ContainsKey (s.Account.Id)) {
                    lock (serverResultsLock) {
                        serverResults [s.Account.Id] = s.Status.GetValue<List<NcEmailMessageIndex>> ();
                    }
                    bool startMerge;
                    lock (lockObject) {
                        startMerge = (null != lastSearch) && !searchInProgress;
                        if (startMerge) {
                            searchInProgress = true;
                        }
                        if (null == nextSearch) {
                            serverResultsPending = true;
                        }
                    }
                    if (startMerge) {
                        StartSearch ();
                    }
                }
            }
        }

        void CancelServerSearches ()
        {
            if (null != accountSearchTokens) {
                foreach (var accountTokenPair in accountSearchTokens) {
                    McPending.Cancel (accountTokenPair.Key, accountTokenPair.Value);
                }
                accountSearchTokens = null;
            }
        }

        private class SearchMatch
        {
            public McEmailMessageThread thread;
            public float matchScore;
        }
    }
}

