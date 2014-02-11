//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
    public class NachoContacts : INachoContacts
    {
        List<McContact> list;

        public NachoContacts ()
        {
            list = BackEnd.Instance.Db.Table<McContact> ().OrderBy (c => c.LastName).ToList ();
        }

        public int Count ()
        {
            return list.Count;
        }

        public McContact GetContact (int i)
        {
            var c = list.ElementAt (i);
            c.ReadAncillaryData (BackEnd.Instance.Db);
            return c;
        }
    }
}
