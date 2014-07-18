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
    /// Used to create lists of name/value pairs
    public class McAbstrContactAttribute : McAbstrObject
    {
        [Indexed]
        public Int64 ContactId { get; set; }

        /// Values are created & displayed in a certain order
        public int Order { get; set; }

        /// Field name
        public string Name { get; set; }

        /// User-defined label if one exists
        public string Label { get; set; }

        public McContact GetContact ()
        {
            return NcModel.Instance.Db.Get<McContact> (ContactId);
        }
    }
}

