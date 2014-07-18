//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McTimeZone : McAbstrObject
    {
        /// The offset from UTC, in minutes;
        public int Bias { get; set; }

        /// Optional TZ description as an array of 32 WCHARs
        public string StandardName { get; set; }

        /// When the transition from DST to standard time occurs
        public System.DateTime StandardDate { get; set; }

        /// Number of minutes to add to Bias during standard time
        public int StandardBias { get; set; }

        /// Optional DST description as an array of 32 WCHARs
        public string DaylightName { get; set; }

        /// When the transition from standard time to DST occurs
        public System.DateTime DaylightDate { get; set; }

        /// Number of miniutes to add to Bias during DST
        public int DaylightBias { get; set; }

        public McTimeZone ()
        {
            LastModified = DateTime.UtcNow;
        }
    }
}

