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

        List<NcEmailMessageIndex> list;
        List<McEmailMessageThread> threadList;

        public NachoMessageSearchResults (List<Index.MatchedItem> matches)
        {
            List<int> adds;
            List<int> deletes;

            list = new List<NcEmailMessageIndex> ();

            foreach (var match in matches) {
                if ("message" == match.Type) {
                    var messageIndex = new NcEmailMessageIndex ();
                    messageIndex.Id = int.Parse (match.Id);
                    list.Add (messageIndex);
                }
            }

            Refresh (out adds, out deletes);
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            if (null == list) {
                list = new List<NcEmailMessageIndex> ();
            }
            if (!NcMessageThreads.AreDifferent (threadList, list, out adds, out deletes)) {
                return false;
            }
            threadList = NcMessageThreads.ThreadByMessage (list);
            return true;
        }

        public int Count ()
        {
            return threadList.Count;
        }

        public McEmailMessageThread GetEmailThread (int i)
        {
            var t = threadList.ElementAt (i);
            return t;
        }

        public string DisplayName ()
        {
            return "Search";
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

