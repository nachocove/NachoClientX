//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using System.Linq;
using System.Collections.Generic;
using Xamarin.Contacts;

namespace NachoClient.iOS
{
    public class DeviceContacts : INachoContacts
    {
        List<Contact> list;

        public DeviceContacts ()
        {
            AddressBook book = new AddressBook ();
            list = book.OrderBy (c => c.LastName).ToList ();
        }

        public int Count ()
        {
            return list.Count;
        }

        public NcContact GetContact (int i)
        {
            var c = list.ElementAt (i);
            var nc = new NcContact ();

            nc.FirstName = c.FirstName;
            nc.LastName = c.LastName;

            return nc;
        }
    }
}

