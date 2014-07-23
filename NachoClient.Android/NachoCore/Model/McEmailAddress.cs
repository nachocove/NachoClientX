//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using SQLite;
using MimeKit;

using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McEmailAddress : McAbstrObjectPerAcc
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

        [Indexed]
        public int Score { get; set; }

        public bool IsHot { get; set; }

        public bool IsVip { get; set; }

        public bool IsBlacklisted { get; set; }

        public int ColorIndex { get; set; }

        public static bool AddOrUpdate (int accountId, string emailAddressString, out McEmailAddress emailAddress)
        {
            InternetAddressList addresses;
            if (!InternetAddressList.TryParse (emailAddressString, out addresses)) {
                emailAddress = null;
                return false;
            }
            if (0 == addresses.Count) {
                emailAddress = null;
                return false;
            }
            NcAssert.True (1 == addresses.Count);
            NcAssert.True (addresses [0] is MailboxAddress);

            return AddOrUpdate (accountId, addresses [0] as MailboxAddress, out emailAddress);
        }

        public static bool AddOrUpdate (int accountId, MailboxAddress mailboxAddress, out McEmailAddress emailAddress)
        {
            bool needsInsert = false;
            bool needsUpdate = false;
            // Does this email address exist, and if not, let's create it
            var query = "SELECT * from McEmailAddress WHERE CanonicalEmailAddress = ?";
            emailAddress = NcModel.Instance.Db.Query<McEmailAddress> (query, mailboxAddress.Address).SingleOrDefault ();
            if (null == emailAddress) {
                needsInsert = true;
                emailAddress = new McEmailAddress (accountId, mailboxAddress.Address);
            }

            if (0 == emailAddress.ColorIndex) {
                needsUpdate = true;
                emailAddress.ColorIndex = NachoPlatform.PlatformUserColorIndex.PickRandomColorForUser ();
            }

            if (needsInsert) {
                emailAddress.Insert ();
            } else if (needsUpdate) {
                emailAddress.Update ();
            }
            return true;
        }
          
    }
}

