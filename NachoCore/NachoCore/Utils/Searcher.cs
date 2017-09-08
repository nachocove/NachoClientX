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

    abstract class Searcher<ResultType>
    {

        public event EventHandler<ResultType> ResultsFound;

        /// <summary>
        /// Search for the given string. Must be called from the UI thread only.
        /// </summary>
        /// <param name="query">The query string typed by the user</param>
        public void Search (string query)
        {
            bool needsSearch = false;
            lock (Lock) {
                UserQuery = query;
                needsSearch = !IsSearching;
            }
            if (needsSearch) {
                NcTask.Run (SearchTask, "Searcher.SearchTask");
            }
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

    }
}