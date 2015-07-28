//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

namespace NachoCore.Utils
{
    /// <summary>
    /// Search helper provides an asynchronous interface for issuing searches.
    /// </summary>
    public class SearchHelper
    {
        public int Version { get; protected set; }

        protected string Description;
        protected Queue<string> SearchQueue;
        protected object LockObj;
        protected Action<string> SearchAction;

        public SearchHelper (string description, Action<string> searchAction)
        {
            LockObj = new object ();
            Description = description;
            SearchQueue = new Queue<string> ();
            SearchAction = searchAction;
        }

        protected void StartSearch (string searchString)
        {
            NcTask.Run (() => {
                SearchAction (searchString);
            }, "SearchHelper_" + Description).ContinueWith ((task) => {
                lock (LockObj) {
                    if (0 < SearchQueue.Count) {
                        string newSearchString = null;
                        while (0 < SearchQueue.Count) {
                            newSearchString = SearchQueue.Dequeue ();
                        }
                        StartSearch (newSearchString);
                    } else {
                        NcAbate.RegularPriority (Description);
                    }
                }
            });
        }

        public void Search (string searchString)
        {
            lock (LockObj) {
                Version += 1;
                if (0 < SearchQueue.Count) {
                    SearchQueue.Enqueue (searchString);
                    return;
                }
                NcAbate.HighPriority (Description);
                StartSearch (searchString);
            }
        }
    }
}

