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

        public string DisplayEmailAddress { get; set; }

        [Indexed] // pre-computed for fast search
        public string DisplayFirstName { get; set; }

        [Indexed] // pre-computed for fast search
        public string DisplayLastName { get; set; }

        public string DisplayName { get; set; }

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

            if (String.IsNullOrEmpty (emailAddress.DisplayEmailAddress)) {
                needsUpdate = true;
                emailAddress.DisplayEmailAddress = mailboxAddress.ToString ();
            }

            if (String.IsNullOrEmpty (emailAddress.DisplayName)) {
                needsUpdate = true;
                emailAddress.DisplayName = mailboxAddress.Name;
            }

            if (String.IsNullOrEmpty (emailAddress.DisplayFirstName) && String.IsNullOrEmpty (emailAddress.DisplayLastName)) {
                string[] items = emailAddress.DisplayName.Split (new char [] { ',', ' ' });
                switch (items.Length) {
                case 2:
                    if (0 < emailAddress.DisplayName.IndexOf (',')) {
                        // Last name, First name
                        needsUpdate = true;
                        emailAddress.DisplayLastName = items [0];
                        emailAddress.DisplayFirstName = items [1];
                    } else {
                        // First name, Last name
                        needsUpdate = true;
                        emailAddress.DisplayFirstName = items [0];
                        emailAddress.DisplayLastName = items [1];
                    }
                    break;
                case 3:
                    if (-1 == emailAddress.DisplayName.IndexOf (',')) {
                        needsUpdate = true;
                        emailAddress.DisplayFirstName = items [0];
                        emailAddress.DisplayLastName = items [2];
                    }
                    break;
                }
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

