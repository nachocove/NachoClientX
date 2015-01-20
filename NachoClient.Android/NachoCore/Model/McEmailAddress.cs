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

        [Indexed]
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
            NcAssert.True (addresses [0] is MailboxAddress);

            return Get (accountId, addresses [0] as MailboxAddress, out emailAddress);
        }

        public static bool Get (int accountId, MailboxAddress mailboxAddress, out McEmailAddress emailAddress)
        {
            McEmailAddress retval = null; // need a local variable for lambda
            NcModel.Instance.RunInTransaction (() => {
                // Does this email address exist, and if not, let's create it
                var query = "SELECT * from McEmailAddress WHERE CanonicalEmailAddress = ?";
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
                    var emailAddressId = Get (accountId, ((MailboxAddress)address).Address);
                    if (0 != emailAddressId) {
                        emailAddressIdList.Add (emailAddressId);
                    }
                }
            }
            return emailAddressIdList;
        }
    }
}

