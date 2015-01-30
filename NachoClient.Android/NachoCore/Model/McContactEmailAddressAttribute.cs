//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class McContactEmailAddressAttribute : McAbstrContactAttribute
    {
        [Indexed] // Reference to McEmailAddress
        public int EmailAddress { get; set; }

        [Indexed] // Email address as it appears in contact record
        public string Value { get; set; }

        public bool IsInList (List<McContactEmailAddressAttribute> addressList)
        {
            foreach (var address in addressList) {
                if (address.EmailAddress == EmailAddress) {
                    return true;
                }
            }
            return false;
        }

        public static bool IsSuperSet (List<McContactEmailAddressAttribute> list1,
                                       List<McContactEmailAddressAttribute> list2)
        {
            if (list1.Count < list2.Count) {
                return false;
            }
            foreach (var addr in list2) {
                if (!addr.IsInList (list1)) {
                    return false;
                }
            }
            return true;
        }
    }
}

