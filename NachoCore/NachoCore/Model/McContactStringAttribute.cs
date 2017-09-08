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
    /// <summary>
    /// Data types stored in the string table.
    /// </summary>
    public enum McContactStringType
    {
        Relationship,
        PhoneNumber,
        IMAddress,
        Category,
        Address,
        Date,
    }

    /// <summary>
    /// String attributes, such as phone numbers, im addresses
    /// </summary>
    public class McContactStringAttribute : McAbstrContactAttribute, ISetComparable<McContactStringAttribute>
    {
        [Indexed]
        public McContactStringType Type { get; set; }

        [Indexed]
        public string Value { get; set; }


        public static bool IsEquivalent (McContactStringAttribute a, McContactStringAttribute b)
        {
            if (a.Type != b.Type) {
                return false;
            }
            // TODO - Find a phone number parsing library that can handle all phone # in the world
            //        and use it to verify if two phone number strings are equivalent
            if (a.Value != b.Value) {
                return false;
            }
            return true;
        }

        public bool IsEquivalent (McContactStringAttribute s)
        {
            return IsEquivalent (this, s);
        }

        public static bool IsSuperSet (List<McContactStringAttribute> list1,
                                       List<McContactStringAttribute> list2)
        {
            return SetHelper<McContactStringAttribute>.IsSuperSet (list1, list2);
        }

        public static List<McContactStringAttribute> QueryByContactIdAndType (int contactId, McContactStringType stringType)
        {
            return NcModel.Instance.Db.Table<McContactStringAttribute> ().Where (x => (contactId == x.ContactId) && (stringType == x.Type)).ToList ();
        }

        public override bool MatchesToken (string token)
        {
            return Value?.StartsWith (token, StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }
}

