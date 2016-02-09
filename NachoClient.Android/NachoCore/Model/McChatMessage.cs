//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McChatMessage : McAbstrObject
    {

        [Indexed]
        public int ChatId { get; set; }
        public int MessageId { get; set; }

        public McChatMessage () : base ()
        {
        }

        public static List<McChatMessage> GetChatMessages (int chatId, int offset = 0, int limit = 50)
        {
            return NcModel.Instance.Db.Query<McChatMessage> (
                "SELECT * FROM McChatMessage cm " +
                "JOIN McEmailMessage m ON cm.MessageId = m.MessageId " +
                "WHERE cm.ChatId = ? " +
                "ORDER BY m.DateReceived DESC OFFSET ? LIMIT ?", chatId, offset, limit);
        }
    }
}

