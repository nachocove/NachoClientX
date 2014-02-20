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
            Refresh ();
        }

        public void Refresh()
        {
            list = BackEnd.Instance.Db.Table<McContact> ().OrderBy (c => c.LastName).ToList ();
            if (null == list) {
                list = new List<McContact> ();
            }
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
