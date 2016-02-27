﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
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

        public static List<McChatMessage> QueryByMessageId (int messageId)
        {
            return NcModel.Instance.Db.Query<McChatMessage> ("SELECT * FROM McChatMessage WHERE MessageId = ?", messageId);
        }
    }
}
