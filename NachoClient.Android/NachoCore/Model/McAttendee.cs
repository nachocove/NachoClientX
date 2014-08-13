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
    /// The attendee table is a big old list of non-unique names.
    /// Each attendee record refers back to its Calendar record or
    /// exception record.
    /// </summary>
    public partial class McAttendee : McAbstrObject
    {
        /// Parent Calendar or Exception item index.
        [Indexed]
        public Int64 ParentId { get; set; }

        public static int CALENDAR = 1;
        public static int EXCEPTION = 2;
        public static int MEETING_REQUEST = 3;

        /// Which table has the parent?
        public int ParentType { get; set; }

        /// Email address of attendee
        [MaxLength (256)]
        public string Email { get; set; }

        /// Display name of attendee
        [MaxLength (256)]
        public string Name { get; set; }

        /// Required, optional, resource
        public NcAttendeeType AttendeeType { get; set; }

        public bool AttendeeTypeIsSet { get; set; }

        /// Unknown, tentative, accept, ...
        public NcAttendeeStatus AttendeeStatus { get; set; }

        public bool AttendeeStatusIsSet { get; set; }

        public McAttendee ()
        {
            Id = 0;
            ParentId = 0;
            ParentType = 0;
            Name = null;
            Email = null;
            AttendeeType = NcAttendeeType.Unknown;
            AttendeeStatus = NcAttendeeStatus.NotResponded;
        }

        public McAttendee (string name, string email, NcAttendeeType type = NcAttendeeType.Unknown, NcAttendeeStatus status = NcAttendeeStatus.NotResponded)
        {
            Id = 0;
            ParentId = 0;
            ParentType = 0;
            Name = name;
            Email = email;
            AttendeeType = type;
            AttendeeTypeIsSet = (NcAttendeeType.Unknown != type);
            AttendeeStatus = status;
            AttendeeStatusIsSet = (NcAttendeeStatus.NotResponded != status);
        }

        public static int GetParentType (McAbstrCalendarRoot r)
        {
            if (r.GetType () == typeof(McCalendar)) {
                return CALENDAR;
            } else if (r.GetType () == typeof(McException)) {
                return EXCEPTION;
            } else if (r.GetType() == typeof(McMeetingRequest)) {
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

        private string displayName;

        [Ignore]
        /// <summary>
        /// Gets the display name.
        /// </summary>
        /// <value>The display name is calculated unless set non-null.</value>
        public string DisplayName {
            get {
                if (!String.IsNullOrEmpty (displayName)) {
                    return displayName;
                }
                if (!String.IsNullOrEmpty (Name)) {
                    return Name;
                }
                if (!String.IsNullOrEmpty (Email)) {
                    return Email;
                }
                NcAssert.CaseError ();
                return "";
            }
            protected set {
                displayName = value;
            }
        }
    }
}

