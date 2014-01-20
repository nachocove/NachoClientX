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

        public NcEmailAddress(Kind k, string a)
        {
            kind = k;
            address = a;
        }

        public enum Kind
        {
            To,
            Cc,
            Bcc,
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
            throw new System.BadImageFormatException ();
        }

        public static string ToPrefix(Kind kind)
        {
            switch (kind) {
            case Kind.To:
                return "To:";
            case Kind.Cc:
                return "Cc:";
            case Kind.Bcc:
                return "Bcc:";
            default:
                NachoAssert.CaseError ();
                return"";
            }
        }
    }
}

