//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    public class NcMessageThreads
    {
        public NcMessageThreads ()
        {
        }

        static public List<McEmailMessageThread> ThreadByConversation (List<NcEmailMessageIndex> list)
        {
            NcAssert.True (null != list);
            var conversationList = new List<McEmailMessageThread> ();
            for (int i = 0; i < list.Count; i++) {
                var singleMessageList = new McEmailMessageThread ();
                singleMessageList.Add (list [i]);
                conversationList.Add (singleMessageList);
            }
            return conversationList;
        }

        // Return true or false if old and new lists are different.
        // Return 'deletes' if a series of deletes can transform oldList into newList.
        protected static bool CheckForDeletes (List<McEmailMessageThread> oldList, List<NcEmailMessageIndex> newList, out List<int> deletes)
        {
            deletes = null;
            if (null == oldList) {
                return (null != newList);
            }
            if (0 == newList.Count) {
                return true;
            }
            // New list has more; need 'adds'
            if (oldList.Count < newList.Count) {
                return true;
            }
            deletes = new List<int> ();
            int oldListIndex = 0;
            int newListIndex = 0;
            while ((oldListIndex < oldList.Count) && (newListIndex < newList.Count)) {
                var messageId = oldList [oldListIndex].GetEmailMessageIndex (0);
                if (messageId != newList [newListIndex].Id) {
                    deletes.Add (oldListIndex);
                } else {
                    newListIndex += 1;
                }
                oldListIndex += 1;
            }
            // Delete the end of list, if any more
            while (oldListIndex < oldList.Count) {
                deletes.Add (oldListIndex);
                oldListIndex += 1;
            }
            // Made it to the end of the lists with no deletes; old & new are the same
            if ((newList.Count == newListIndex) && (oldList.Count == oldListIndex)) {
                if (0 == deletes.Count) {
                    deletes = null;
                    return false;
                }
            }
            // Didn't get to the end of the new list, some adds at the end
            if (newList.Count != newListIndex) {
                deletes = null;
            }
            return true;
        }

        // Return true or false if old and new lists are different.
        // Return 'adds' if the new list is strictly additions to the old list
        protected static bool CheckForAdds (List<McEmailMessageThread> oldList, List<NcEmailMessageIndex> newList, out List<int> adds)
        {
            adds = null;
            if (null == oldList) {
                return (null != newList);
            }
            if (0 == newList.Count) {
                return true;
            }
            // Old list has more; need 'deletes'
            if (oldList.Count > newList.Count) {
                return true;
            }
            adds = new List<int> ();
            int oldListIndex = 0;
            int newListIndex = 0;

            while ((oldListIndex < oldList.Count) && (newListIndex < newList.Count)) {
                var oldId = oldList [oldListIndex].GetEmailMessageIndex (0);
                var newId = newList [newListIndex].Id;
                if (oldId != newId) {
                    adds.Add (newListIndex);
                } else {
                    oldListIndex += 1;
                }
                newListIndex += 1;
            }

            // Adds the end of list, if any more
            while (newListIndex < newList.Count) {
                adds.Add (newListIndex);
                newListIndex += 1;
            }
            // Made it to the end of the lists with no adds; old & new are the same
            if ((newList.Count == newListIndex) && (oldList.Count == oldListIndex)) {
                if (0 == adds.Count) {
                    adds = null;
                    return false;
                }
            }
            // Didn't get to the end of the old list, some deletes at the end
            if (oldList.Count != oldListIndex) {
                adds = null;
            }
            return true;
        }

        // Return true if lists differ. Return a list of additions or deletions
        public static bool AreDifferent (List<McEmailMessageThread> oldList, List<NcEmailMessageIndex> newList, out List<int> adds, out List<int> deletes)
        {
            adds = null;
            deletes = null;
            if (!CheckForDeletes (oldList, newList, out deletes)) {
                return false;
            }
            if (null == deletes) {
                CheckForAdds (oldList, newList, out adds);
            }
            return true;
        }
    }
}
//        var map = new Dictionary <string, List<McEmailMessage>>();
//
//        for (int i = 0; i < list.Count; i++) {
//        var message = list [i];
//        if (null == message.MessageID) {
//        continue;
//        }
//        List<McEmailMessage> messageList = null;
//        if (map.TryGetValue (message.MessageID, out messageList)) {
//        // Duplicate!
//        messageList.Add (message);
//        list [i] = null;
//        continue;
//        }
//        map [message.MessageID] = new List<McEmailMessage> ();
//        }
//
//        // No duplicates left in the list.
//
//        for (int i = 0; i < list.Count; i++) {
//        var message = list [i];
//        if (null != message.InReplyTo) {
//        if (RedirectMap (map, message, message.InReplyTo)) {
//        list [i] = null;
//        goto done;
//        }
//        }
//        if (null != message.References) {
//        string[] references = message.References.Split (new char[] { '\n' });
//        foreach (var reference in references) {
//        if (RedirectMap (map, message, reference)) {
//        list [i] = null;
//        goto done;
//        }
//        }
//        }
//        done:
//        continue;
//        }
//        threadList = new List<List<McEmailMessage>> ();
//        for (int i = 0; i < list.Count; i++) {
//        if (null == list [i]) {
//        continue;
//        }
//        var message = list [i];
//        if (null == message.MessageID) {
//        var singleMessageList = new List<McEmailMessage> ();
//        singleMessageList.Add (message);
//        threadList.Add (singleMessageList);
//        continue;
//        } else {
//        var l = map [message.MessageID];
//        l.Add (message);
//        threadList.Add (l);
//        }
//        }
//        }
//
//        bool RedirectMap(Dictionary <string, List<McEmailMessage>> map, McEmailMessage message, string messageID)
//        {
//        List<McEmailMessage> messageList;
//        if (map.TryGetValue (messageID, out messageList)) {
//        if (null != message.MessageID) {
//        messageList.AddRange (map [message.MessageID]);
//        map [message.MessageID] = messageList;
//        }
//        messageList.Add (message);
//        return true;
//        }
//        return false;

