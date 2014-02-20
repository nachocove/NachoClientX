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
            Refresh ();
        }

        public void Refresh()
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
            var nc = new McContact (McContact.McContactSource.Device);

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
                    nc.AddPhoneNumberAttribute ("Work", p.Label ?? "Work", p.Number);
                    break;
                case PhoneType.WorkFax:
                    nc.AddPhoneNumberAttribute ("WorkFax", p.Label ?? "Work Fax", p.Number);
                    break;
                case PhoneType.Home:
                    nc.AddPhoneNumberAttribute ("Home", p.Label ?? "Home", p.Number);
                    break;
                case PhoneType.HomeFax:
                    nc.AddPhoneNumberAttribute ("HomeFax", p.Label ?? "Home Fax", p.Number);
                    break;
                case PhoneType.Pager:
                    nc.AddPhoneNumberAttribute ("Pager", p.Label ?? "Pager", p.Number);
                    break;
                case PhoneType.Mobile:
                    nc.AddPhoneNumberAttribute ("Mobile", p.Label ?? "Mobile", p.Number);
                    break;
                case PhoneType.Other:
                    nc.AddPhoneNumberAttribute ("Other", p.Label ?? "Other", p.Number);
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

