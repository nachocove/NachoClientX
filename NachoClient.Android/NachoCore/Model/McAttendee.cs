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
    public partial class McAttendee : McAbstrObjectPerAcc
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

        /// McEmailAddress index of Email
        public int EmailAddressId { get; set; }

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

        public McAttendee (int accountId, string name, string email, NcAttendeeType type = NcAttendeeType.Unknown, NcAttendeeStatus status = NcAttendeeStatus.NotResponded)
        {
            AccountId = accountId;
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
            } else if (r.GetType () == typeof(McMeetingRequest)) {
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

        private NcEmailAddress.Kind GetAddressMapType ()
        {
            switch (AttendeeType) {
            case NcAttendeeType.Unknown:
                return NcEmailAddress.Kind.Unknown;
            case NcAttendeeType.Optional:
                return NcEmailAddress.Kind.Optional;
            case NcAttendeeType.Required:
                return NcEmailAddress.Kind.Required;
            case NcAttendeeType.Resource:
                return NcEmailAddress.Kind.Resource;
            default:
                throw new NcAssert.NachoDefaultCaseFailure (
                    String.Format ("Unknown attendee type {0}", (int)AttendeeType));
            }
        }

        private void InsertAddressMap ()
        {
            var map = CreateAddressMap ();
            map.EmailAddressId = EmailAddressId;
            map.AddressType = GetAddressMapType ();
            map.Insert ();
        }

        private void DeleteAddressMap ()
        {
            // Delete all 3 address types (required, optional, resources) so that in case there
            // is a change of AttendeeType, we still clean up the old map
            McMapEmailAddressEntry.DeleteAttendeeMapEntries (AccountId, Id);
        }

        public override int Insert ()
        {
            using (var capture = CaptureWithStart ("Insert")) {
                int retval = 0;
                NcModel.Instance.RunInTransaction (() => {
                    EmailAddressId = McEmailAddress.Get (AccountId, Email);
                    retval = base.Insert ();
                    InsertAddressMap ();
                });
                return retval;
            }
        }

        public override int Update ()
        {
            using (var capture = CaptureWithStart ("Update")) {
                int retval = 0;
                NcModel.Instance.RunInTransaction (() => {
                    EmailAddressId = McEmailAddress.Get (AccountId, Email);
                    DeleteAddressMap ();
                    InsertAddressMap ();
                    retval = base.Update ();
                });
                return retval;
            }
        }

        public override int Delete ()
        {
            using (var capture = CaptureWithStart ("Delete")) {
                int retval = 0;
                NcModel.Instance.RunInTransaction (() => {
                    retval = base.Delete ();
                    DeleteAddressMap ();
                });
                return retval;
            }
        }
    }
}

