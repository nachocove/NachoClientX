//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public class NcMessageThreads
    {
        public NcMessageThreads ()
        {
        }

        static public List<List<McEmailMessage>> ThreadByConversation (List<McEmailMessage> list)
        {
            NachoAssert.True (null != list);
            var conversationList = new List<List<McEmailMessage>> ();
            for (int i = 0; i < list.Count; i++) {
                var singleMessageList = new List<McEmailMessage> ();
                singleMessageList.Add (list [i]);
                conversationList.Add (singleMessageList);
            }
            return conversationList;
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

