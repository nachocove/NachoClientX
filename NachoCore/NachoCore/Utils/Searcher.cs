//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;

using NachoCore.Index;
using NachoCore.Model;
using NachoPlatform;
using System.Collections.Concurrent;

using System.Threading;

namespace NachoCore.Utils
{

    public abstract class Searcher<ResultType>
    {

        public event EventHandler<ResultType> ResultsFound;

        /// <summary>
        /// Search for the given string. Must be called from the UI thread only.
        /// </summary>
        /// <param name="query">The query string typed by the user</param>
        public virtual void Search (string query)
        {
            bool needsSearch = false;
            lock (Lock) {
                UserQuery = query;
                needsSearch = !IsSearching;
            }
            if (string.IsNullOrEmpty (UserQuery)) {
                StopListeningForStatusInd ();
            } else {
                StartListeningForStatusInd ();
            }
            if (needsSearch) {
                NcTask.Run (SearchTask, "Searcher.SearchTask");
            }
        }

        public virtual void Cleanup ()
        {
            StopListeningForStatusInd ();
        }

        string UserQuery;
        string SearchedQuery;

        bool IsSearching;

        object Lock = new object ();

        void SearchTask ()
        {
            ResultType results;
            do {
                lock (Lock) {
                    SearchedQuery = UserQuery;
                }
                results = SearchResults (SearchedQuery);
                lock (Lock) {
                    if (SearchedQuery == UserQuery) {
                        IsSearching = false;
                        break;
                    }
                }
            } while (true);

            InvokeOnUIThread.Instance.Invoke (() => {
                ResultsFound?.Invoke (this, results);
            });
        }

        protected abstract ResultType SearchResults (string query);

        protected virtual NcResult.SubKindEnum IndexChangedStatus {
            get {
                return NcResult.SubKindEnum.NotSpecified;
            }
        }

        bool IsListeningForStatusInd;

        /// <summary>
        /// Start listening for status events matching <see cref="IndexChangedStatus"/>, but only
        /// if IndexChangedStatus is something other than <see cref="NcResult.SubKindEnum.NotSpecified"/>.
        /// We're interested in auto re-running a search when the index has been updated because that means
        /// the results may have changed
        /// </summary>
		void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd && IndexChangedStatus != NcResult.SubKindEnum.NotSpecified) {
                NcApplication.Instance.StatusIndEvent += StatusIndHandler;
                IsListeningForStatusInd = true;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent -= StatusIndHandler;
                IsListeningForStatusInd = false;
            }
        }

        void StatusIndHandler (object sender, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;
            if (statusEvent.Status.SubKind == IndexChangedStatus) {
                if (!string.IsNullOrEmpty (UserQuery)) {
                    Search (UserQuery);
                }
            }
        }
    }

    public abstract class ServerSearcher<ResultType>
    {
        public event EventHandler<ResultType> ResultsFound;

        protected abstract NcResult.SubKindEnum SuccessStatus { get; }
        protected abstract NcResult.SubKindEnum ErrorStatus { get; }

        Dictionary<int, string> ServerSearchTokens = new Dictionary<int, string> ();

        string Query;

        public virtual void Search (string query)
        {
            StopServerSearch ();
            Query = query;
            StartServerSearch ();
        }

        public void Stop ()
        {
            StopServerSearch ();
        }

        public virtual void Cleanup ()
        {
            StopServerSearch ();
        }

        void StartServerSearch ()
        {
            StartListeningForStatusInd ();
            ServerSearchTokens = CreateServerSearchTokens (Query);
        }

        protected abstract Dictionary<int, string> CreateServerSearchTokens (string query);

        void StopServerSearch ()
        {
            StopListeningForStatusInd ();
            foreach (var pair in ServerSearchTokens) {
                McPending.Cancel (pair.Key, pair.Value);
            }
            ServerSearchTokens.Clear ();
        }

        bool IsListeningForStatusInd;

        void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent += StatusIndHandler;
                IsListeningForStatusInd = true;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent -= StatusIndHandler;
                IsListeningForStatusInd = false;
            }
        }

        void StatusIndHandler (object sender, EventArgs e)
        {
            if (!IsListeningForStatusInd) {
                return;
            }
            var statusEvent = (StatusIndEventArgs)e;
            if (statusEvent.Account == null) {
                return;
            }
            if (!ServerSearchTokens.ContainsKey (statusEvent.Account.Id)) {
                return;
            }
            var accountToken = ServerSearchTokens [statusEvent.Account.Id];
            var tokens = statusEvent.Tokens;
            if (tokens == null || !tokens.Contains (accountToken)) {
                return;
            }
            if (statusEvent.Status.SubKind == SuccessStatus) {
                ServerSearchTokens.Remove (statusEvent.Account.Id);
                HandleStatus (statusEvent.Status);
            } else if (statusEvent.Status.SubKind == ErrorStatus) {
                ServerSearchTokens.Remove (statusEvent.Account.Id);
            }
        }

        protected abstract void HandleStatus (NcResult status);

        protected void FoundResults (ResultType results)
        {
            ResultsFound?.Invoke (this, results);
        }

    }
}