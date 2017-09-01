//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using MimeKit;
using NachoCore;
using NachoCore.Model;
using System.Linq;

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

        public enum Kind
        {
            Unknown = 0,
            // McEmailMessage
            To = 1,
            Cc = 2,
            Bcc = 3,
            Sender = 4,
            From = 5,
            // McAttendee
            Required = 6,
            Optional = 7,
            Resource = 8,
            // McCalendar
            Organizer = 9
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
            if (prefix.StartsWith ("From")) {
                return Kind.From;
            }
            if (prefix.StartsWith ("Sender")) {
                return Kind.Sender;
            }
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
            case Kind.From:
                return "From";
            case Kind.Sender:
                return "Sender";
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
                return "";
            }
        }

        public static Kind ToKind (NcAttendeeType attendeeType)
        {
            switch (attendeeType) {
            case NcAttendeeType.Unknown:
                return Kind.Unknown;
            case NcAttendeeType.Optional:
                return Kind.Optional;
            case NcAttendeeType.Required:
                return Kind.Required;
            case NcAttendeeType.Resource:
                return Kind.Resource;
            default:
                NcAssert.CaseError (String.Format ("Unknown NcAttendeeType {0}", (int)attendeeType));
                break;
            }
            return Kind.Unknown;
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
            case Kind.Optional:
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

        public MailboxAddress ToMailboxAddress (bool mustUseAddress = false)
        {
            // Must have a contact or an address
            NcAssert.True ((null != this.contact) || (null != this.address));

            string candidate;
            MailboxAddress mailbox;

            if (mustUseAddress || (null == this.contact)) {
                candidate = this.address;
            } else {
                candidate = this.contact.GetEmailAddress ();
            }

            if (!MailboxAddress.TryParse (candidate, out mailbox)) {
                Log.Error (Log.LOG_EMAIL, "Mailbox candidate won't parse: {0}", candidate);
                return null;
            }

            if (null == mailbox.Address) {
                Log.Error (Log.LOG_EMAIL, "Mailbox candidate has null address: {0}", candidate);
                return null;
            }

            if (string.IsNullOrEmpty (mailbox.Name) && null != this.contact) {
                mailbox.Name = this.contact.GetDisplayName ();
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

        public static List<NcEmailAddress> ParseAddressListString (string addressString,
                                                                   Kind addressKind)
        {
            List<NcEmailAddress> addressList = new List<NcEmailAddress> ();
            if (null == addressString) {
                return addressList;
            }
            InternetAddressList inetAddressList;
            if (!InternetAddressList.TryParse (addressString, out inetAddressList)) {
                return addressList;
            }
            foreach (var inetAddress in inetAddressList.Mailboxes) {
                NcEmailAddress address = new NcEmailAddress (addressKind, inetAddress.ToString ());
                addressList.Add (address);
            }
            return addressList;
        }

        public static List<NcEmailAddress> ParseToAddressListString (string toAddressString)
        {
            return ParseAddressListString (toAddressString, Kind.To);
        }

        public static List<NcEmailAddress> ParseCcAddressListString (string ccAddressString)
        {
            return ParseAddressListString (ccAddressString, Kind.Cc);
        }

        public static List<NcEmailAddress> ParseBccAddressListString (string bccAddressString)
        {
            return ParseAddressListString (bccAddressString, Kind.Bcc);
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

        public static InternetAddressList ToInternetAddressList (List<NcEmailAddress> addressList, Kind kind)
        {
            var list = new InternetAddressList ();
            if (null == addressList) {
                return list;
            }
            foreach (var a in addressList) {
                NcAssert.True (kind == a.kind);
                var mailbox = a.ToMailboxAddress (mustUseAddress: true);
                if (null != mailbox) {
                    list.Add (mailbox);
                }
            }
            return list;
        }
    }
}

