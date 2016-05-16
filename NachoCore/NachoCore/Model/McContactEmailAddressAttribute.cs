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
    public class McContactEmailAddressAttribute : McAbstrContactAttribute, ISetComparable<McContactEmailAddressAttribute>
    {
        [Indexed] // Reference to McEmailAddress
        public int EmailAddress { get; set; }

        [Indexed] // Email address as it appears in contact record
        public string Value { get; set; }

        public bool IsEquivalent (McContactEmailAddressAttribute address)
        {
            return EmailAddress == address.EmailAddress;
        }

        public static bool IsSuperSet (List<McContactEmailAddressAttribute> list1,
                                       List<McContactEmailAddressAttribute> list2)
        {
            return SetHelper<McContactEmailAddressAttribute>.IsSuperSet (list1, list2);
        }
    }
}

