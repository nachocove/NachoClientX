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
    public class McContactStringAttribute : McAbstrContactAttribute
    {
        [Indexed]
        public McContactStringType Type { get; set; }

        [Indexed]
        public string Value { get; set; }
    }
}

