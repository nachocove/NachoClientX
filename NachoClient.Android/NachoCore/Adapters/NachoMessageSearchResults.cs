//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;

namespace NachoCore
{
    public class NachoMessageSearchResults : INachoEmailMessages
    {

        List<McEmailMessageThread> list;
        List<McEmailMessageThread> threadList;

        List<Index.MatchedItem> matches;
        List<McEmailMessageThread> serverMatches;

        public NachoMessageSearchResults ()
        {
            threadList = new List<McEmailMessageThread> ();
        }

        public void UpdateMatches (List<Index.MatchedItem> matches)
        {
            this.matches = matches;
            UpdateResults ();
        }

        public void UpdateServerMatches (List<McEmailMessageThread> serverMatches)
        {
            this.serverMatches = serverMatches;
            UpdateResults ();
        }

        public void UpdateResults ()
        {
            List<int> adds;
            List<int> deletes;

            list = new List<McEmailMessageThread> ();

            if (null != matches) {
                foreach (var match in matches) {
                    if ("message" == match.Type) {
                        var thread = new McEmailMessageThread ();
                        thread.FirstMessageId = int.Parse (match.Id);
                        thread.MessageCount = 1;
                        list.Add (thread);
                    }
                }
            }
            if (null != serverMatches) {
                foreach (var serverMatch in serverMatches) {
                    if (!list.Contains (serverMatch, new McEmailMessageThreadIndexComparer ())) {
                        list.Add (serverMatch);
                    }
                }
            }

            list.RemoveAll ((McEmailMessageThread obj) => IsValid (obj));

            Refresh (out adds, out deletes);
        }

        // As messages are moved, they change index & become
        // unavailable.  Deferred messages should be hidden.
        protected bool IsValid (McEmailMessageThread messageIndex)
        {
            var message = messageIndex.FirstMessageSpecialCase ();
            if ((null == message) || message.IsDeferred ()) {
                return true;
            }
            return false;
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var threads = NcMessageThreads.ThreadByMessage (list);
            if (NcMessageThreads.AreDifferent (threadList, threads, out adds, out deletes)) {
                threadList = threads;
                return true;
            }
            return false;
        }

        public int Count ()
        {
            return threadList.Count;
        }

        public McEmailMessageThread GetEmailThread (int i)
        {
            var t = threadList.ElementAt (i);
            t.Source = this;
            return t;
        }

        public List<McEmailMessageThread> GetEmailThreadMessages (int id)
        {
            var thread = new List<McEmailMessageThread> ();
            var m = new McEmailMessageThread ();
            m.FirstMessageId = id;
            m.MessageCount = 1;
            thread.Add (m);
            return thread;
        }

        public string DisplayName ()
        {
            return "Search";
        }

        public bool HasOutboxSemantics ()
        {
            return false;
        }

        public bool HasDraftsSemantics ()
        {
            return false;
        }

        public void StartSync ()
        {

        }

        public INachoEmailMessages GetAdapterForThread (string threadId)
        {
            return null;
        }
    }
}

