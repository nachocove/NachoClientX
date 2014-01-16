//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
    public class NachoEmailMessages : INachoEmailMessages
    {
        List<McEmailMessage> list;

        public NachoEmailMessages (McFolder folder)
        {
            list = BackEnd.Instance.Db.Table<McEmailMessage> ().Where(c => c.FolderId == folder.Id).OrderByDescending (c => c.DateReceived).ToList ();
        }

        public int Count ()
        {
            return list.Count;
        }

        public McEmailMessage GetEmailMessage (int i)
        {
            var m = list.ElementAt (i);
            return m;
        }
    }
}
