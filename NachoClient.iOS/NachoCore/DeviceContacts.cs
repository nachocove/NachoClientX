//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using System.Linq;
using System.Collections.Generic;
using Xamarin.Contacts;

namespace NachoCore
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

        public McContact GetContact (int i)
        {
            var c = list.ElementAt (i);
            var nc = new McContact ();

            nc.Title = c.Prefix;
            nc.FirstName = c.FirstName;
            nc.MiddleName = c.MiddleName;
            nc.LastName = c.LastName;
            nc.Suffix = c.Suffix;
            nc.NickName = c.Nickname;

            foreach (Relationship r in c.Relationships) {
                ;
            }

            foreach (Phone p in c.Phones) {
                switch (p.Type) {
                case PhoneType.Work:
                    nc.BusinessPhoneNumber = p.Number;
                    break;
                case PhoneType.WorkFax:
                    nc.BusinessFaxNumber = p.Number;
                    break;
                case PhoneType.Home:
                    nc.HomePhoneNumber = p.Number;
                    break;
                case PhoneType.HomeFax:
                    nc.HomeFaxNumber = p.Number;
                    break;
                case PhoneType.Pager:
                    nc.PagerNumber = p.Number;
                    break;
                case PhoneType.Mobile:
                    nc.MobilePhoneNumber = p.Number;
                    break;
                case PhoneType.Other:
                    nc.Business2PhoneNumber = p.Number;
                    break;
                default:
                    System.Diagnostics.Trace.Fail ("GetContact unhandled enumeration");
                    break;
                }
            }

            return nc;
        }
    }
}

