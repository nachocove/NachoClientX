//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore
{
    /// <summary>
    /// An email address is represented by the string
    /// address and may include a matching McContact.
    /// </summary>
    public class NcEmailAddress
    {
        public NcEmailAddress (Kind k)
        {
            kind = k;
        }

        public NcEmailAddress (Kind k, string a)
        {
            kind = k;
            address = a;
        }

        public NcEmailAddress (McAttendee attendee)
        {
            this.kind = FromAttendeeType (attendee.AttendeeType);
            this.address = attendee.Email;
        }

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
        /// Value as entered; never null
        public string address;
        /// Matching contact; maybe null
        public McContact contact;
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
                return "To:";
            case Kind.Cc:
                return "Cc:";
            case Kind.Bcc:
                return "Bcc:";
            case Kind.Required:
                return "Required";
            case Kind.Optional:
                return "Optional:";
            case Kind.Resource:
                return "Resource:";
            case Kind.Unknown:
                return "Unkown";
            default:
                NachoAssert.CaseError ();
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
                NachoAssert.CaseError ();
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
                NachoAssert.CaseError ();
                return NcAttendeeType.Unknown;
            }
        }

    }
}

