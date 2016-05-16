//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.Model
{
    /// <summary>
    /// The category table represents a collection of categories
    /// assigned to a calendar or exception item.
    /// </summary>
    public partial class McCalendarCategory : McAbstrObjectPerAcc
    {
        /// Parent Calendar or Exception item index.
        [Indexed]
        public Int64 ParentId { get; set; }

        public static int CALENDAR = 1;
        public static int EXCEPTION = 2;
        public static int MEETING_REQUEST = 3;
        // Which table has the parent?
        public int ParentType { get; set; }

        /// Name of category
        [MaxLength (256)]
        public string Name { get; set; }

        public McCalendarCategory ()
        {
            Id = 0;
            ParentId = 0;
            ParentType = 0;
            Name = null;
        }

        public McCalendarCategory (int accountId, string name) : this ()
        {
            AccountId = accountId;
            Name = name;
        }

        public static int GetParentType (McAbstrCalendarRoot r)
        {
            if (r.GetType () == typeof(McCalendar)) {
                return CALENDAR;
            } else if (r.GetType () == typeof(McException)) {
                return EXCEPTION;
            } else if(r.GetType() == typeof(McMeetingRequest)) {
                return MEETING_REQUEST;
            } else {
                NcAssert.True (false);
                return 0;
            }
        }

        public void SetParent (McAbstrCalendarRoot r)
        {
            ParentId = r.Id;
            ParentType = GetParentType (r);
        }
    }
}

