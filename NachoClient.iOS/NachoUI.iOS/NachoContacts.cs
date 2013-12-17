//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections.Generic;
using NachoCore;
using NachoCore.Model;
using System.Linq;

namespace NachoClient.iOS
{
    public class NachoContacts : INachoContacts
    {

        AppDelegate appDelegate { get; set; }

        List<NcContact> list;

        public NachoContacts ()
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
            list = appDelegate.Be.Db.Table<NcContact> ().OrderBy (c => c.LastName).ToList();
       }

        public int Count()
        {
            return list.Count;
        }

        public NcContact GetContact(int i)
        {
            return list.ElementAt(i);
        }

    }
}
