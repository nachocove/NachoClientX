//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;


//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.IO;
using System.Linq;
//using System.Net.Http;
using SQLite;

//using NachoCore;
//using NachoCore.Utils;
//using MimeKit;
//using System.Xml.Linq;
//using NachoCore.Model;
//using NachoCore.Brain;
//using NachoCore.Index;
//using NachoCore.ActiveSync;

namespace NachoCore.Model
{

    public class McEmailMessageNeedsUpdate : McAbstrObjectPerAcc
    {
        [Indexed]
        public int EmailMessageId { get; set; }

        [Indexed]
        public int NeedsUpdate { get; set; }

        public static void Insert (McEmailMessage message, int value)
        {
            var c = new McEmailMessageNeedsUpdate ();
            c.AccountId = message.AccountId;
            c.EmailMessageId = message.Id;
            c.NeedsUpdate = 0;
            c.Insert ();
        }

        public static void Update(McEmailMessage message, int value)
        {
            NcModel.Instance.Db.Query<McEmailMessageNeedsUpdate> ("UPDATE McEmailMessageNeedsUpdate SET NeedsUpdate = ? WHERE EmailMessageId = ?", value, message.Id);
        }

        public static void Delete (McEmailMessage message)
        {
            NcModel.Instance.Db.Query<McEmailMessageNeedsUpdate> ("DELETE FROM McEmailMessageNeedsUpdate WHERE EmailMessageId = ?", message.Id);
        }

        public static int Get(McEmailMessage message)
        {
            return NcModel.Instance.Db.Query<McEmailMessageNeedsUpdate> ("SELECT * FROM McEmailMessageNeedsUpdate WHERE EmailMessageId = ?", message.Id).First ().NeedsUpdate;
        }
            
    }
}

