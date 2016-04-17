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
                var oldMessageIdIndexes = new Dictionary<int, int> (oldItems.Count);
                var newMessageIdIndexes = new Dictionary<int, int> (newItems.Count);
                var oldMessageIds = new List<int> (oldItems.Count);
                var newMessageIds = new List<int> (newItems.Count);
                int i = 0;
                int messageId;
                foreach (var thread in oldItems) {
                    messageId = thread.FirstMessageSpecialCaseIndex ();
                    oldMessageIdIndexes [messageId] = i;
                    oldMessageIds.Add (messageId);
                    ++i;
                }
                i = 0;
                foreach (var thread in newItems) {
                    messageId = thread.FirstMessageSpecialCaseIndex ();
                    newMessageIdIndexes [messageId] = i;
                    newMessageIds.Add (messageId);
                    ++i;
                }
                var deletedMessageIds = oldMessageIds.Except (newMessageIds);
                var addedMessageIds = newMessageIds.Except (oldMessageIds);
                foreach (var messageId_ in deletedMessageIds){
                    deletes.Add (oldMessageIdIndexes [messageId_]);
                }
                foreach (var messageId_ in addedMessageIds) {
                    adds.Add (newMessageIdIndexes[messageId_]);
                }
            }
            return adds.Count > 0 || deletes.Count > 0;
        }
    }
}
