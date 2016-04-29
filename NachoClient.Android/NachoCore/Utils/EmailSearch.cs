//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoCore.Index;

namespace NachoCore.Utils
{
    public class EmailSearch : NachoEmailMessages
    {
        public delegate void UpdateUiAction (string searchString, List<McEmailMessageThread> results);

        UpdateUiAction updateUi;

        object lockObject = new object ();
        object serverResultsLock = new object();

        bool searchInProgress = false;
        string lastSearch = "";
        string nextSearch = null;
        bool serverResultsPending = false;

        List<McAccount> accounts;
        Dictionary<int, string> accountSearchTokens;

        List<MatchedItem> indexResults;
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
            indexResults = new List<MatchedItem> ();
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

        public override bool Refresh (out List<int> adds, out List<int> deletes)
        {
            // TODO  Should this rerun the search??

            adds = null;
            deletes = null;
            return false;
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
                        var indexMatches = new List<MatchedItem> ();
                        if (!string.IsNullOrEmpty (searchString)) {
                            int maxResults = Math.Min (100 * searchString.Length, 1000);
                            foreach (var account in accounts) {
                                indexMatches.AddRange (new NcIndex (NcModel.Instance.GetIndexPath (account.Id)).SearchAllEmailMessageFields (searchString, maxResults));
                            }
                        }
                        indexResults = indexMatches;
                    }

                    // Merge the index results and server results, avoiding duplicates and bad items.
                    // Score the matches, and sort from highest to lowest score.
                    var searchWords = searchString.Trim ().Split (new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var dbIds = new HashSet<int> ();
                    var messageIds = new HashSet<string> ();
                    var scoredResults = new List<SearchMatch> ();

                    NcAssert.NotNull (indexResults, "StartSearch: indexResults is null");
                    foreach (var indexMatch in indexResults) {
                        NcAssert.NotNull (indexMatch, "StartSearch: indexMatch is null");
                        try {
                            float matchScore;
                            int id = int.Parse (indexMatch.Id);
                            if (ScoreMessage (id, searchWords, indexMatch.Score, out matchScore, dbIds, messageIds)) {
                                scoredResults.Add (new SearchMatch () {
                                    thread = new McEmailMessageThread () {
                                        FirstMessageId = id,
                                        MessageCount = 1,
                                    },
                                    matchScore = matchScore,
                                });
                            }
                        } catch (FormatException) {
                            Log.Error (Log.LOG_SEARCH, "Index search returned an item with a malformed id: {0}", indexMatch.Id);
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

        bool ScoreMessage (int messageId, string[] searchWords, float indexScore, out float score, HashSet<int> dbIds, HashSet<string> messageIds)
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
            foreach (string word in searchWords) {
                localScore += ScoreForField (message.From, word, 3);
                localScore += ScoreForField (message.Subject, word, 2);
                localScore += ScoreForField (message.To, word);
                localScore += ScoreForField (message.Cc, word);
                localScore += ScoreForField (message.Bcc, word);
            }

            score = (float)localScore + indexScore;
            return true;
        }

        int ScoreForField (string field, string word, int multiplier = 1)
        {
            if (null != field && field.ToLower ().Contains (word.ToLower ())) {
                return word.Length * multiplier;
            }
            return 0;
        }

        void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_EmailSearchCommandSucceeded == s.Status.SubKind &&
                null != s.Account && null != s.Tokens && null != accountSearchTokens &&
                accountSearchTokens.Keys.Contains (s.Account.Id) &&
                s.Tokens.Contains (accountSearchTokens [s.Account.Id]))
            {
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

