//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McChatParticipant : McAbstrObject
    {

        [Indexed]
        public int ChatId { get; set; }
        public int EmailAddrId { get; set; }
        public int ContactId { get; set; }

        public McChatParticipant () : base ()
        {
        }

        public static List<McChatParticipant> GetChatParticipants (int chatId)
        {
            return NcModel.Instance.Db.Query<McChatParticipant> ("SELECT * FROM McChatParticipant WHERE ChatId = ?", chatId);
        }
    }
}

