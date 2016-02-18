//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McChatMessage : McAbstrObjectPerAcc
    {

        [Indexed]
        public int ChatId { get; set; }
        [Indexed]
        public int MessageId { get; set; }

        public McChatMessage () : base ()
        {
        }

        public static List<McEmailMessage> GetChatMessages (int chatId, int offset = 0, int limit = 50)
        {
            return NcModel.Instance.Db.Query<McEmailMessage> (
                "SELECT m.* FROM McChatMessage cm " +
                "JOIN McEmailMessage m ON cm.MessageId = m.Id " +
                "WHERE cm.ChatId = ? " +
                "AND likelihood (m.IsAwaitingDelete = 0, 1.0) " +
                "ORDER BY m.DateReceived DESC LIMIT ? OFFSET ?", chatId, limit, offset);
        }

        public static List<McChatMessage> QueryByMessageId (int messageId)
        {
            return NcModel.Instance.Db.Query<McChatMessage> ("SELECT * FROM McChatMessage WHERE MessageId = ?", messageId);
        }
    }
}

