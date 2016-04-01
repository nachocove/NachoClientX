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

        public static bool Get (int accountId, string emailAddressString, out McEmailAddress emailAddress)
        {
            emailAddress = null;
            if (String.IsNullOrEmpty (emailAddressString)) {
                return false;
            }
            InternetAddressList addresses;
            if (!InternetAddressList.TryParse (emailAddressString, out addresses)) {
                return false;
            }
            if (0 == addresses.Count) {
                return false;
            }
            NcAssert.True (1 == addresses.Count);
            if (!(addresses [0] is MailboxAddress)) {
                return false; // TODO: group addresses
            }
            return Get (accountId, addresses [0] as MailboxAddress, out emailAddress);
        }

        /// <summary>
        /// Find the McEmailAddress with the given e-mail address, creating a new McEmailAddress if necessary.
        /// </summary>
        public static bool Get (int accountId, MailboxAddress mailboxAddress, out McEmailAddress emailAddress)
        {
            // See if a matching McEmailAddress exists, without opening a write transaction.
            var query = "SELECT * from McEmailAddress WHERE CanonicalEmailAddress = ?";
            McEmailAddress retval = NcModel.Instance.Db.Query<McEmailAddress> (query, mailboxAddress.Address).SingleOrDefault ();
            if (null != retval) {
                emailAddress = retval;
                return true;
            }
            NcModel.Instance.RunInTransaction (() => {
                // Repeat the lookup while within the transaction, in case another thread added it just now.
                // If it is still not found, create a new McEmailaddress.
                retval = NcModel.Instance.Db.Query<McEmailAddress> (query, mailboxAddress.Address).SingleOrDefault ();
                if (null == retval) {
                    retval = new McEmailAddress (accountId, mailboxAddress.Address);
                    retval.ColorIndex = NachoPlatform.PlatformUserColorIndex.PickRandomColorForUser ();
                    retval.Insert ();
                }
            });
            emailAddress = retval;
            return true;
        }

        public static int Get (int accountId, string emailAddressString)
        {
            McEmailAddress emailAddress;
            if (!Get (accountId, emailAddressString, out emailAddress)) {
                return 0;
            }
            return emailAddress.Id;
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

