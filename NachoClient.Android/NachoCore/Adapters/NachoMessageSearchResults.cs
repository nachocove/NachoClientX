//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public class NachoMessageSearchResults : INachoEmailMessages
    {
        int accountId;
        List<McEmailMessageThread> list;
        List<McEmailMessageThread> threadList;

        List<Index.MatchedItem> matches;
        List<McEmailMessageThread> serverMatches;

        bool changed = false;

        object syncObj = new object();

        public NachoMessageSearchResults (int accountId)
        {
            this.accountId = accountId;
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

        public void ClearServerMatches()
        {
            this.serverMatches = null;
        }

        public void UpdateResults ()
        {
            lock (syncObj) {
                List<int> adds;
                List<int> deletes;

                var combined = new List<McEmailMessageThread> ();

                if (null != matches) {
                    foreach (var match in matches) {
                        if ("message" == match.Type) {
                            var thread = new McEmailMessageThread ();
                            thread.FirstMessageId = int.Parse (match.Id);
                            thread.MessageCount = 1;
                            combined.Add (thread);
                        }
                    }
                }
                if (null != serverMatches) {
                    foreach (var serverMatch in serverMatches) {
                        if (!combined.Contains (serverMatch, new McEmailMessageThreadIndexComparer ())) {
                            combined.Add (serverMatch);
                        }
                    }
                }

                var messageIdSet = new HashSet<string> ();

                combined.RemoveAll ((McEmailMessageThread messageIndex) => {
                    // As messages are moved, they change index & become
                    // unavailable.  Deferred messages should be hidden.
                    var message = messageIndex.FirstMessageSpecialCase ();
                    if ((null == message) || message.IsDeferred ()) {
                        return true;
                    }
                    if (String.IsNullOrEmpty (message.MessageID)) {
                        return false;
                    }
                    return !messageIdSet.Add (message.MessageID);
                });

                // Sort by date
                var idList = new List<int> (messageIdSet.Count);
                foreach (var m in combined) {
                    idList.Add (m.FirstMessageId);
                }
                list = McEmailMessage.QueryForMessageThreadSet (idList);

                changed = true;
                Refresh (out adds, out deletes);
            }
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            lock (syncObj) {
                if (!changed) {
                    adds = null;
                    deletes = null;
                    return false;
                }
                var threads = NcMessageThreads.ThreadByMessage (list);
                if (NcMessageThreads.AreDifferent (threadList, threads, out adds, out deletes)) {
                    threadList = threads;
                    return true;
                }
                return false;
            }
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

        public NcResult StartSync ()
        {
            return NachoSyncResult.DoesNotSync ();
        }

        public INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            return null;
        }

        public bool IsCompatibleWithAccount (McAccount account)
        {
            return account.Id == accountId;
        }

    }
}

