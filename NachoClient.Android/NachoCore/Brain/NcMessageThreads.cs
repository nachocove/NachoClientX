//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Brain;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    public class NcMessageThreads
    {
        public NcMessageThreads ()
        {
        }

        static public List<McEmailMessageThread> ThreadByConversation (List<McEmailMessageThread> list)
        {
            if (null == list) {
                return new List<McEmailMessageThread> ();
            } else {
                return list;
            }
        }

        static public List<McEmailMessageThread> ThreadByMessage (List<McEmailMessageThread> list)
        {
            if (null == list) {
                return new List<McEmailMessageThread> ();
            } else {
                return list;
            }
        }

        public static bool AreDifferent (List<McEmailMessageThread> oldItems, List<McEmailMessageThread> newItems, out List<int> adds, out List<int> deletes)
        {
            adds = new List<int> ();
            deletes = new List<int> ();
            if (oldItems == null || oldItems.Count == 0) {
                if (newItems != null) {
                    for (int i = 0; i < newItems.Count; ++i) {
                        adds.Add (i);
                    }
                }
            } else if (newItems == null || newItems.Count == 0) {
                if (oldItems != null) {
                    for (int i = 0; i < oldItems.Count; ++i) {
                        deletes.Add (i);
                    }
                }
            } else {
                var oldIndexesByMessageId = new Dictionary<int, int> (oldItems.Count);
                int oldIndex = 0;
                int messageId;
                foreach (var thread in oldItems) {
                    messageId = thread.FirstMessageSpecialCaseIndex ();
                    oldIndexesByMessageId [messageId] = oldIndex;
                    ++oldIndex;
                }
                int newIndex = 0;
                foreach (var thread in newItems) {
                    messageId = thread.FirstMessageSpecialCaseIndex ();
                    if (!oldIndexesByMessageId.ContainsKey (messageId)) {
                        adds.Add (newIndex);
                    } else {
                        oldIndexesByMessageId.Remove (messageId);
                    }
                    ++newIndex;
                }
                foreach (var entry in oldIndexesByMessageId) {
                    deletes.Add (entry.Value);
                }
            }
            return adds.Count > 0 || deletes.Count > 0;
        }
    }
}
