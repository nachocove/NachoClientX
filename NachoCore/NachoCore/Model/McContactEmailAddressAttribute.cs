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

        private McEmailAddress _CachedAddress;
        public McEmailAddress CachedAddress {
            get {
                if (_CachedAddress == null) {
                    _CachedAddress = McEmailAddress.QueryById<McEmailAddress> (EmailAddress);
                }
                return _CachedAddress;
            }
        }

        public bool IsEquivalent (McContactEmailAddressAttribute address)
        {
            return EmailAddress == address.EmailAddress;
        }

        public static bool IsSuperSet (List<McContactEmailAddressAttribute> list1,
                                       List<McContactEmailAddressAttribute> list2)
        {
            return SetHelper<McContactEmailAddressAttribute>.IsSuperSet (list1, list2);
        }

        public override bool MatchesToken (string token)
        {
            if (Value == null) {
                return false;
            }
            if (Value.StartsWith (token, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            var atIndex = Value.IndexOf ('@');
            if (atIndex >= 0) {
                if (Value.Substring (atIndex + 1).StartsWith (token, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }
    }
}

