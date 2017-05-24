//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NachoCore.Utils;
using SQLite;

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

        private static int ToInt (string s, int defaultValue = 0)
        {
            int result;
            if (int.TryParse(s, out result)) {
                return result;
            }
            return defaultValue;
        }

        private const int SERIALIZATION_VERSION = 1;

        // Keeping the tags short results in a measurable performance improvement
        // when a meeting has lots of attendees.
        private const string ATTENDEES_TAG = "A";
        private const string VERSION_ATTRIBUTE = "v";
        private const string ATTENDEE_TAG = "a";
        private const string EMAIL_TAG = "e";
        private const string NAME_TAG = "n";
        private const string TYPE_TAG = "t";
        private const string STATUS_TAG = "s";

        internal static string Serialize (IEnumerable<McAttendee> attendees)
        {
            var xRoot = new XElement (ATTENDEES_TAG);
            xRoot.SetAttributeValue (VERSION_ATTRIBUTE, SERIALIZATION_VERSION);
            foreach (var attendee in attendees) {
                var xAttendee = new XElement (ATTENDEE_TAG,
                    new XElement (EMAIL_TAG, attendee.Email),
                    new XElement (NAME_TAG, attendee.Name));
                if (attendee.AttendeeTypeIsSet) {
                    xAttendee.Add (new XElement (TYPE_TAG, (int)attendee.AttendeeType));
                }
                if (attendee.AttendeeStatusIsSet) {
                    xAttendee.Add (new XElement (STATUS_TAG, (int)attendee.AttendeeStatus));
                }
                xRoot.Add (xAttendee);
            }
            // Don't worry about indenting or other white space in the XML string.
            // That just wastes space in the database.
            return xRoot.ToString (SaveOptions.DisableFormatting);
        }

        internal static List<McAttendee> Deserialize (string xmlString)
        {
            var result = new List<McAttendee> ();
            XElement xRoot = null;
            try {
                xRoot = XElement.Parse (xmlString);
            } catch (Exception e) {
                Log.Error (Log.LOG_CALENDAR, "Serialized attendee string couldn't be parsed as XML: {0}: {1}", e.GetType ().Name, e.Message);
                return result;
            }
            if (ATTENDEES_TAG != xRoot.Name.LocalName) {
                Log.Error (Log.LOG_CALENDAR, "Serialized attendee string has the wrong root tag, {0} instead of {1}", xRoot.Name.LocalName, ATTENDEES_TAG);
                return result;
            }
            if (null == xRoot.Attribute (VERSION_ATTRIBUTE)) {
                Log.Error (Log.LOG_CALENDAR, "Serialized attendee string doesn't have a {0} attribute on the root element.", VERSION_ATTRIBUTE);
                return result;
            }
            if (SERIALIZATION_VERSION != ToInt (xRoot.Attribute (VERSION_ATTRIBUTE).Value, -1)) {
                Log.Error (Log.LOG_CALENDAR, "Serialized attendee string has an unexpected value for the version attribute: {0}", xRoot.Attribute (VERSION_ATTRIBUTE).Value);
                return result;
            }
            foreach (var xAttendee in xRoot.Elements ()) {
                if (ATTENDEE_TAG != xAttendee.Name.LocalName) {
                    Log.Error (Log.LOG_CALENDAR, "Serialized attendee string has a <{0}> element where <{1}> was expected.", xAttendee.Name.LocalName, ATTENDEE_TAG);
                    continue;
                }
                var attendee = new McAttendee ();
                foreach (var child in xAttendee.Elements ()) {
                    switch (child.Name.LocalName) {
                    case EMAIL_TAG:
                        attendee.Email = child.Value;
                        break;
                    case NAME_TAG:
                        attendee.Name = child.Value;
                        break;
                    case TYPE_TAG:
                        attendee.AttendeeTypeIsSet = true;
                        attendee.AttendeeType = (NcAttendeeType)ToInt (child.Value, (int)NcAttendeeType.Unknown);
                        break;
                    case STATUS_TAG:
                        attendee.AttendeeStatusIsSet = true;
                        attendee.AttendeeStatus = (NcAttendeeStatus)ToInt (child.Value, (int)NcAttendeeStatus.NotResponded);
                        break;
                    default:
                        Log.Error (Log.LOG_CALENDAR, "Serialized attendee string has an unexpected element <{0}>", child.Name.LocalName);
                        break;
                    }
                }
                result.Add (attendee);
            }
            return result;
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

        public McContact GetContact ()
        {
            var contacts = McContact.QueryByEmailAddress (AccountId, Email);
            foreach (var contact in contacts) {
                if (contact.PortraitId != 0) {
                    return contact;
                }
            }
            if (contacts.Count > 0) {
                return contacts [0];
            }
            return null;
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

