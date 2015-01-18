//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using MimeKit;

using NachoCore.Utils;

namespace NachoCore.Model
{
    public class NcMigration2 : NcMigration
    {
        public NcMigration2 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            // We only count the # of invalid ToEmailAddressId. But in reality, we will migrate
            // both To and Cc.
            return Db.Table<McEmailMessage> ().Where (x => x.MigrationVersion < 1).Count ();
        }

        public void ProcessAddressList (McEmailMessage emailMessage, string addressString, EmailAddressType addressType)
        {
            var addressList = NcEmailAddress.ParseAddressListString (addressString);
            var thisVersion = Version ();
            foreach (MailboxAddress address in addressList) {
                McEmailAddress emailAddress;
                var got = McEmailAddress.Get (emailMessage.AccountId, address.Address, out emailAddress);
                NcAssert.True (got);

                var map = new McMapEmailMessageAddress ();
                map.EmailMessageId = emailMessage.Id;
                map.EmailAddressId = emailAddress.Id;
                map.AddressType = addressType;
                // don't use the current version as there may be migration between this and current version
                map.MigrationVersion = thisVersion;
                map.Insert ();
            }
        }

        public override void Run (CancellationToken token)
        {
            var thisVersion = Version ();
            foreach (var emailMessage in Db.Table<McEmailMessage> ().Where (x => x.MigrationVersion < thisVersion)) {
                token.ThrowIfCancellationRequested ();

                NcModel.Instance.RunInTransaction (() => {
                    if (!String.IsNullOrEmpty (emailMessage.To)) {
                        ProcessAddressList (emailMessage, emailMessage.To, EmailAddressType.TO);
                    }
                    if (!String.IsNullOrEmpty (emailMessage.Cc)) {
                        ProcessAddressList (emailMessage, emailMessage.Cc, EmailAddressType.CC);
                    }
                    emailMessage.MigrationVersion = thisVersion;
                    emailMessage.Update ();
                    NcMigration.ProcessedObjects += 1;
                });
            }
        }
    }
}

