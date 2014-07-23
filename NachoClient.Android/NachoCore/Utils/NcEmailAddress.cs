//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using MimeKit;
using NachoCore;
using NachoCore.Model;

namespace NachoCore.Utils
{
    /// <summary>
    /// An email address is represented by the string
    /// address and may include a matching McContact.
    /// </summary>
    public class NcEmailAddress
    {
        /// How is this NcEmailAddress being used?
        public enum Action
        {
            undefined,
            create,
            edit,
        };

        /// In what list does this NcEmailAddress reside?
        public enum Kind
        {
            To,
            Cc,
            Bcc,
            Required,
            Optional,
            Resource,
            Unknown,
        };

        /// Which list?
        public Kind kind;
        /// At what index
        public int index;
        /// For what reason
        public Action action;
        /// Value as entered; never null
        public string address;
        /// Matching contact; maybe null
        public McContact contact;

        /// <summary>
        /// Create a basic NcEmailAddress
        /// </summary>
        public NcEmailAddress (Kind kind)
        {
            this.kind = kind;
            this.action = Action.undefined;

        }

        /// <summary>
        /// Create a basic NcEmailAddress
        /// </summary>
        public NcEmailAddress (Kind kind, string address)
        {
            this.kind = kind;
            this.address = address;
            this.action = Action.undefined;
            this.index = -1;
        }

        /// <summary>
        /// Create a basic NcEmailAddress
        /// </summary>
        public NcEmailAddress (McAttendee attendee)
        {
            NcAssert.True (attendee.AttendeeTypeIsSet);
            this.kind = FromAttendeeType (attendee.AttendeeType);
            this.address = attendee.Email;
            this.action = Action.undefined;
            this.index = -1;
        }
        // TODO: Localize!
        public static Kind ToKind (string prefix)
        {
            if (prefix.StartsWith ("To")) {
                return Kind.To;
            }
            if (prefix.StartsWith ("Cc")) {
                return Kind.Cc;
            }
            if (prefix.StartsWith ("Bcc")) {
                return Kind.Bcc;
            }
            if (prefix.StartsWith ("Req")) {
                return Kind.Required;
            }
            if (prefix.StartsWith ("Opt")) {
                return Kind.Optional;
            }
            if (prefix.StartsWith ("Res")) {
                return Kind.Resource;
            }
            if (prefix.StartsWith ("Unk")) {
                return Kind.Unknown;
            }

            throw new System.BadImageFormatException ();
        }

        public static string ToPrefix (Kind kind)
        {
            switch (kind) {
            case Kind.To:
                return "To";
            case Kind.Cc:
                return "Cc";
            case Kind.Bcc:
                return "Bcc";
            case Kind.Required:
                return "Required";
            case Kind.Optional:
                return "Optional";
            case Kind.Resource:
                return "Resource";
            case Kind.Unknown:
                return "Unknown";
            default:
                NcAssert.CaseError ();
                return"";
            }
        }

        public static Kind FromAttendeeType (NcAttendeeType type)
        {
            switch (type) {
            case NcAttendeeType.Required:
                return Kind.Required;
            case NcAttendeeType.Optional:
                return Kind.Optional;
            case NcAttendeeType.Resource:
                return Kind.Resource;
            case NcAttendeeType.Unknown:
                return Kind.Unknown;
            default:
                NcAssert.CaseError ();
                return Kind.Unknown;
            }
        }

        public static NcAttendeeType ToAttendeeType (Kind kind)
        {
            switch (kind) {
            case Kind.Required:
                return NcAttendeeType.Required;
            case  Kind.Optional:
                return NcAttendeeType.Optional;
            case Kind.Resource:
                return NcAttendeeType.Resource;
            case Kind.Unknown:
                return NcAttendeeType.Unknown;
            default:
                NcAssert.CaseError ();
                return NcAttendeeType.Unknown;
            }
        }

        public MailboxAddress ToMailboxAddress ()
        {
            // Must have a contact or an address
            NcAssert.True ((null != this.contact) || (null != this.address));

            string candidate;
            MailboxAddress mailbox;

            if (null != this.contact) {
                candidate = this.contact.GetEmailAddress();
            } else {
                candidate = this.address;
            }

            if (!MailboxAddress.TryParse (candidate, out mailbox)) {
                Log.Error (Log.LOG_EMAIL, "Mailbox candidate won't parse: {0}", candidate);
                return null;
            }

            if (null == mailbox.Address) {
                Log.Error (Log.LOG_EMAIL, "Mailbox candidate has null address: {0}", candidate);
                return null;
            }

            if (null == mailbox.Name) {
                mailbox.Name = this.contact.GetDisplayName();
            }

            return mailbox;
        }

        /// <summary>
        /// Parses the address list string.
        /// </summary>
        /// <returns>A list of InternetAddresses</returns>
        /// <param name="emailAddressString">Email address string.</param>
        public static InternetAddressList ParseAddressListString (string emailAddressString)
        {
            if (null == emailAddressString) {
                return new InternetAddressList ();
            }
            InternetAddressList addresses;
            if (InternetAddressList.TryParse (emailAddressString, out addresses)) {
                return addresses;
            } else {
                return new InternetAddressList ();
            }
        }

        /// <summary>
        /// Parses the mailbox address string.
        /// </summary>
        /// <returns>Null or a single MailboxAddress</returns>
        public static MailboxAddress ParseMailboxAddressString (string emailAddressString)
        {
            var addresses = ParseAddressListString (emailAddressString);
            if (null == addresses) {
                return null;
            }
            if (1 == addresses.Count) {
                return addresses [0] as MailboxAddress;
            } else {
                return null;
            }
        }

        public static void SplitName (MailboxAddress address, ref McContact contact)
        {
            // Try to parse the display name into first / middle / last name
            string[] items = address.Name.Split (new char [] { ',', ' ' });
            switch (items.Length) {
            case 2:
                if (0 < address.Name.IndexOf (',')) {
                    // Last name, First name
                    contact.LastName = items [0];
                    contact.FirstName = items [1];
                } else {
                    // First name, Last name
                    contact.FirstName = items [0];
                    contact.LastName = items [1];
                }
                break;
            case 3:
                if (-1 == address.Name.IndexOf (',')) {
                    contact.FirstName = items [0];
                    contact.MiddleName = items [1];
                    contact.LastName = items [2];
                }
                break;
            }
        }
    }
}

