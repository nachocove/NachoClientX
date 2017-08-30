//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using SQLite;
using MimeKit;

using NachoCore.Utils;

namespace NachoCore.Model
{
    public partial class McEmailAddress : McAbstrObjectPerAcc
    {
        public McEmailAddress ()
        {
        }

        public McEmailAddress (int AccountId, string CanonicalEmailAddress)
        {
            this.CanonicalEmailAddress = CanonicalEmailAddress;
            this.AccountId = AccountId;
        }

        [Indexed]
        public string CanonicalEmailAddress { get; set; }

        public bool IsVip { get; set; }

        public bool IsBlacklisted { get; set; }

        public int ColorIndex { get; set; }

        /// <summary>
        /// Get or create an email address record given the canonical email address string, which is an address
        /// normalized using all lowercase for the domain part, which is case insensitive.  This only checks for
        /// a null/empty address and does no formal validation of the address.  It assumes that the address
        /// came from something like a <see cref="Mailbox"/> that was created by parsing and validating a string.
        /// </summary>
        /// <returns><c>true if the address is not null or empty</c>, <c>false</c> otherwise</returns>
        /// <param name="accountId">Account id</param>
        /// <param name="canonicalEmailAddress">Email address normalized with lowercase domain part</param>
        /// <param name="address">The found or created address</param>
        static bool GetOrCreate (int accountId, string canonicalEmailAddress, out McEmailAddress address)
        {
            if (String.IsNullOrEmpty (canonicalEmailAddress)) {
                address = null;
                return false;
            }
            address = QueryByCanonicalAddress (canonicalEmailAddress);
            if (address != null) {
                return true;
            }
            McEmailAddress addedAddress = null;
            NcModel.Instance.RunInTransaction (() => {
                // Repeat the lookup while within the transaction, in case another thread added it just now.
                // If it is still not found, create a new McEmailaddress.
                addedAddress = QueryByCanonicalAddress (canonicalEmailAddress);
                if (addedAddress == null) {
                    addedAddress = new McEmailAddress (accountId, canonicalEmailAddress);
                    addedAddress.ColorIndex = NachoPlatform.PlatformUserColorIndex.PickRandomColorForUser ();
                    addedAddress.Insert ();
                }
            });
            address = addedAddress;
            return true;
        }

        /// <summary>
        /// Get or creates an email address for the given mailbox.  Returns true if an existing email address
        /// record was found or created, false if the mailbox contains a null address.
        /// </summary>
        /// <returns><c>true</c>, if found or created, <c>false</c> otherwise.</returns>
        /// <param name="accountId">Account id</param>
        /// <param name="mailbox">Mailbox.</param>
        /// <param name="address">The found or created address</param>
        public static bool GetOrCreate (int accountId, Mailbox mailbox, out McEmailAddress address)
        {
            return GetOrCreate (accountId, mailbox.CanonicalAddress, out address);
        }

        /// <summary>
        /// Get or create an email address record after parsing the input string as a mailbox.  This can be
        /// passed a string that includes a name and email, like <c>Some Person &lt;some.person@example.com></c>,
        /// or it can be passed a simple email address.  If the string could not be parsed, this will return
        /// <c>false</c>.
        /// </summary>
        /// <returns><c>true if the string is a valid mailbox</c>, <c>false</c> otherwise</returns>
        /// <param name="accountId">Account id</param>
        /// <param name="emailAddressString">Email or mailbox string</param>
        /// <param name="emailAddress">The found or created address record</param>
        public static bool Get (int accountId, string emailAddressString, out McEmailAddress emailAddress)
        {
            if (Mailbox.TryParse (emailAddressString, out var mailbox)) {
                return GetOrCreate (accountId, mailbox, out emailAddress);
            }
            emailAddress = null;
            return false;
        }

        /// <summary>
        /// Get or create the ID of the email address record that matches the given email or mailbox string.
        /// </summary>
        /// <returns>The id of the found or created address record, or <c>0</c> if the input is not a valid email or mailbox string</returns>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="emailAddressString">Email address string.</param>
        public static int Get (int accountId, string emailAddressString)
        {
            if (Get (accountId, emailAddressString, out var address)) {
                return address.Id;
            }
            return 0;
        }

        /// <summary>
        /// Find the McEmailAddress with the given e-mail address, creating a new McEmailAddress if necessary.
        /// </summary>
        public static bool Get (int accountId, MailboxAddress mailboxAddress, out McEmailAddress emailAddress)
        {
            // TODO: deprecate this in favor of the Maibox apis
            // FIXME: mailboxAddress.Address may have capitals in the domain and therefore not be entirely
            // canonical, but this is how it has always worked
            return GetOrCreate (accountId, mailboxAddress.Address, out emailAddress);
        }

        public static List<int> GetList (int accountId, string emailAddressListString)
        {
            List<int> emailAddressIdList = new List<int> ();
            if (!String.IsNullOrEmpty (emailAddressListString)) {
                var addressList = NcEmailAddress.ParseAddressListString (emailAddressListString);
                foreach (var address in addressList) {
                    if (!(address is MailboxAddress)) {
                        continue; // ignore group address
                    }
                    var emailAddressId = Get (accountId, ((MailboxAddress)address).Address);
                    if (0 != emailAddressId) {
                        emailAddressIdList.Add (emailAddressId);
                    }
                }
            }
            return emailAddressIdList;
        }

        public static McEmailAddress QueryByCanonicalAddress (string canonicalAddress)
        {
            return NcModel.Instance.Db.Query<McEmailAddress> (
                "SELECT * from McEmailAddress WHERE CanonicalEmailAddress = ?",
                canonicalAddress).SingleOrDefault ();
        }

        public static List<McEmailAddress> QueryToCcAddressByMessageId (int messageId)
        {
            return NcModel.Instance.Db.Query<McEmailAddress> (
                "SELECT a.* FROM McEmailAddress AS a " +
                " JOIN McMapEmailAddressEntry AS m ON a.Id = m.EmailAddressId " +
                " WHERE (m.AddressType = ? OR m.AddressType = ?) AND (m.ObjectId = ?) " +
                " GROUP BY a.Id",
                NcEmailAddress.Kind.To, NcEmailAddress.Kind.Cc, messageId);
        }

        public static List<McEmailAddress> QueryToAddressesByMessageId (int messageId)
        {
            return NcModel.Instance.Db.Query<McEmailAddress> (
                "SELECT a.* FROM McEmailAddress AS a " +
                " JOIN McMapEmailAddressEntry AS m ON a.Id = m.EmailAddressId " +
                " WHERE m.AddressType = ? AND (m.ObjectId = ?) " +
                " GROUP BY a.Id",
                NcEmailAddress.Kind.To, messageId);
        }

        public static List<McEmailAddress> QueryCcAddressesByMessageId (int messageId)
        {
            return NcModel.Instance.Db.Query<McEmailAddress> (
                "SELECT a.* FROM McEmailAddress AS a " +
                " JOIN McMapEmailAddressEntry AS m ON a.Id = m.EmailAddressId " +
                " WHERE m.AddressType = ? AND (m.ObjectId = ?) " +
                " GROUP BY a.Id",
                NcEmailAddress.Kind.Cc, messageId);
        }
    }
}

