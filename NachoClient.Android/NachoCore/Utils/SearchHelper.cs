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
                    SearchQueue.Dequeue (); // remove the current search string
                    if (0 < SearchQueue.Count) {
                        // There is still another search queued up
                        while (1 < SearchQueue.Count) {
                            SearchQueue.Dequeue ();
                        }
                        StartSearch (SearchQueue.Peek ());
                    }
                }
            });
        }

        public void Search (string searchString)
        {
            lock (LockObj) {
                Version += 1;
                SearchQueue.Enqueue (searchString);
                if (1 < SearchQueue.Count) {
                    return;
                }
                StartSearch (searchString);
            }
        }
    }
}

