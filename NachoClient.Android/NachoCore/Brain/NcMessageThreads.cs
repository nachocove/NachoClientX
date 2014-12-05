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

        // Return true if lists differ. Return a list of deletions
        public static bool AreDifferent (List<McEmailMessageThread> oldList, List<NcEmailMessageIndex> newList, out List<int> deletes)
        {
            deletes = null;
            if (null == oldList) {
                return (null != newList);
            }
            // New list has nore; we don't handle additions yet
            if (oldList.Count < newList.Count) {
                return true;
            }
            deletes = new List<int> ();
            int oldListIndex = 0;
            int newListIndex = 0;
            while((oldListIndex < oldList.Count) && (newListIndex < newList.Count)) {
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
            // Made it to the end of the lists with no deletes
            if((newList.Count == newListIndex) && (oldList.Count == oldListIndex)) {
                if (0 == deletes.Count) {
                    deletes = null;
                    return false;
                }
            }
            // Didn't get to the end of the new list, some adds at the end
            if (newList.Count != newListIndex) {
                deletes = null;
            }
            // Bug -- iOS crash is resulting list is empty
            if (0 == newList.Count) {
                deletes = null;
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

