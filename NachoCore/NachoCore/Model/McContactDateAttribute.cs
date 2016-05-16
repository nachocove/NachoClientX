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
    /// Date attributes such as birthdays and anniversaries
    /// </summary>
    public class McContactDateAttribute : McAbstrContactAttribute
    {
        public DateTime Value { get; set; }
    }
}

