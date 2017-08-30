//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;

namespace NachoCore
{

    /// <summary>
    /// A name/address pair that represents a mailbox address used in email messages for To, Cc, etc.
    /// Intended to be a simpler replacement for <see cref="Utils.NcEmailAddress"/>, which loses information and 
    /// contains unimportant type information.
    /// </summary>
    /// <remarks>
    /// This basically just wraps <see cref="MimeKit.MailboxAddress"/>.  While we could use use MimeKit directly,
    /// since this class is intended to be used in many places, wrapping allows us to change the underlying library
    /// without having to change types in all of those places.
    /// </remarks>
    public struct Mailbox
    {

        /// <summary>
        /// Create a mailbox using just an email address, leaving the name <c>null</c>
        /// </summary>
        /// <param name="address">The email address without a name</param>
        public Mailbox (string address) : this (new MimeKit.MailboxAddress ((string)null, address))
        {
        }

        /// <summary>
        /// Create a mailbox using a name and email address
        /// </summary>
        /// <param name="name">The display name</param>
        /// <param name="address">The actual email address</param>
        public Mailbox (string name, string address) : this (new MimeKit.MailboxAddress (name, address))
        {
        }

        /// <summary>
        /// Private helper constructor to create a message using the internal <see cref="MimeKit.MailboxAddress"/> data
        /// </summary>
        /// <param name="mimeKitMailbox">MIME kit mailbox.</param>
        Mailbox (MimeKit.MailboxAddress mimeKitMailbox)
        {
            MimeKitMailbox = mimeKitMailbox;
        }

        /// <summary>
        /// Parse the given string into a mailbox.  If the string is not a mailbox or is more than one mailbox,
        /// parsing will fail.
        /// </summary>
        /// <returns><c>true</c>, if the string could be parsed into a mailbox, <c>false</c> otherwise.</returns>
        /// <param name="mailboxString">The string to parse</param>
        /// <param name="mailbox">Returns the parsed mailbox, if succesful</param>
        public static bool TryParse (string mailboxString, out Mailbox mailbox)
        {
            if (mailboxString != null && MimeKit.MailboxAddress.TryParse (mailboxString, out var mimeKitMailbox)) {
                mailbox = new Mailbox (mimeKitMailbox);
                return true;
            }
            mailbox = new Mailbox ();
            return false;
        }

        /// <summary>
        /// Parse the given string into a list of mailboxes.  If the list includes any groups,
        /// they will be silently dropped, and only the top level mailbox addresses will be included
        /// in the result.
        /// </summary>
        /// <returns><c>true</c>, if string could be parsed, <c>false</c> otherwise.</returns>
        /// <param name="mailboxesString">The string to parse</param>
        /// <param name="mailboxes">Returns the list of mailboxes, if successful</param>
        public static bool TryParseArray (string mailboxesString, out Mailbox [] mailboxes)
        {
            if (mailboxesString != null && MimeKit.InternetAddressList.TryParse (mailboxesString, out var internetAddresses)) {
                var mailboxList = new List<Mailbox> (internetAddresses.Count);
                foreach (var address in internetAddresses) {
                    if (address is MimeKit.MailboxAddress) {
                        mailboxList.Add (new Mailbox (address as MimeKit.MailboxAddress));
                    }
                }
                mailboxes = mailboxList.ToArray ();
                return true;
            }
            mailboxes = new Mailbox [0];
            return false;
        }

        /// <summary>
        /// The private backing data
        /// </summary>
        readonly MimeKit.MailboxAddress MimeKitMailbox;

        /// <summary>
        /// Gets the display name for the mailbox, typically a person's first and last name
        /// </summary>
        /// <remarks>
        /// This struct is intended to be read only since mailboxes are such simple data structures
        /// that are mostly just passed around and not edited.  Typically it is lists of mailboxes
        /// that are edited, with each individual mailbox constructed as necessary.
        /// 
        /// IMPORTANT: If we ever want to make this class editable, the set methods must create
        /// a copy of the underlying <see cref="MimeKitMailbox"/>, otherwise copies of this struct
        /// that point to the same underlying data will be modified unexpectedly.
        /// </remarks>
        public string Name {
            get {
                return MimeKitMailbox?.Name;
            }
        }

        /// <summary>
        /// Gets the email address for the mailbox
        /// </summary>
        /// <value>The address.</value>
        /// <remarks>
        /// This struct is intended to be read only since mailboxes are such simple data structures
        /// that are mostly just passed around and not edited.  Typically it is lists of mailboxes
        /// that are edited, with each individual mailbox constructed as necessary.
        /// 
        /// IMPORTANT: If we ever want to make this class editable, the set methods must create
        /// a copy of the underlying <see cref="MimeKitMailbox"/>, otherwise copies of this struct
        /// that point to the same underlying data will be modified unexpectedly.
        /// </remarks>
        public string Address {
            get {
                return MimeKitMailbox?.Address;
            }
        }

        /// <summary>
        /// The canonical address is <see cref="Address"/>, but with the domain part always lowercase.
        /// </summary>
        /// <value>The canonical address.</value>
        public string CanonicalAddress {
            get {
                if (MimeKitMailbox == null || MimeKitMailbox.Address == null) {
                    return null;
                }
                var address = MimeKitMailbox.Address;
                var atIndex = address.IndexOf ('@');
                if (atIndex < 0) {
                    return address;
                }
                return address.Substring (0, atIndex) + address.Substring (atIndex).ToLowerInvariant ();
            }
        }

        /// <summary>
        /// Get one or two uppercase initals from the <see cref="Name"/>, falling back to the first letter of
        /// <see cref="Address"/> if needed.
        /// </summary>
        /// <value>The initials.</value>
        public string Initials {
            get {
                var contact = new Model.McContact ();
                contact.SetName (Name);
                var initails = contact.Initials;
                if (!String.IsNullOrEmpty (initails)) {
                    return initails;
                }
                return Address?.GetFirstLetterOrDigit () ?? "";
            }
        }

        /// <summary>
        /// A string representation of the mailbox suitable for addressing an email message
        /// </summary>
        /// <returns>The formatted mailbox suitable for addressing an email message</returns>
        public override string ToString ()
        {
            return MimeKitMailbox?.ToString ();
        }
    }

    public static class MailboxArrayExtensions
    {
        /// <summary>
        /// A string representation of the mailbox list suitable for email addressing
        /// </summary>
        /// <returns>A comma separated list of mailbox addresses</returns>
        /// <param name="mailboxes">Mailboxes.</param>
        public static string ToAddressString (this Mailbox [] mailboxes)
        {
            return string.Join (",", mailboxes.Select (mailbox => mailbox.ToString ()));
        }
    }
}
