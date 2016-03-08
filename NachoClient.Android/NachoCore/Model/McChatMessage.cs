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
        public string MimeMessageId { get; set; }
        public bool IsLatestDuplicate { get; set; }

        public McChatMessage () : base ()
        {
        }

        public static List<McChatMessage> QueryByMessageId (int messageId)
        {
            return NcModel.Instance.Db.Query<McChatMessage> ("SELECT * FROM McChatMessage WHERE MessageId = ?", messageId);
        }

        public static NcChatMessage EmailMessageInChat (int chatId, int messageId)
        {
            var messages = NcModel.Instance.Db.Query<NcChatMessage> ("SELECT m.*, cm.ChatId FROM McChatMessage cm JOIN McEmailMessage m ON cm.MessageId = m.Id WHERE cm.ChatId = ? AND cm.MessageId = ?", chatId, messageId);
            if (messages.Count > 0) {
                return messages [0];
            }
            return null;
        }

        public void UpdateLatestDuplicate ()
        {
            if (IsLatestDuplicate){
                var chat = McChat.QueryById<McChat> (ChatId);
                chat.UpdateLatestDuplicate (MimeMessageId);
            }
        }
    }
        
    // This class is only for querying McEmailMessages with extra info from McChatMessage
    // It should not be added to the database
    public class NcChatMessage : McEmailMessage
    {
        public int ChatId { get; set; }
    }
}

