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

        public int ProcessAddress (int accountId, int objectId, MailboxAddress address, EmailAddressType addressType)
        {
            McEmailAddress emailAddress;
            var got = McEmailAddress.Get (accountId, address.Address, out emailAddress);
            NcAssert.True (got);

            var map = new McMapEmailAddressEntry ();
            map.ObjectId = objectId;
            map.EmailAddressId = emailAddress.Id;
            map.AddressType = addressType;
            // Override the default current version as there may be migration between this and current version
            map.MigrationVersion = Version ();
            map.Insert ();

            return emailAddress.Id;
        }

        public int ProcessAddress (int accountId, int objectId, string addressString, EmailAddressType addressType)
        {
            if (String.IsNullOrEmpty (addressString)) {
                return 0;
            }
            var address = NcEmailAddress.ParseMailboxAddressString (addressString);
            return ProcessAddress (accountId, objectId, address, addressType);
        }

        public void ProcessAddressList (int accountId, int objectId, string addressString, EmailAddressType addressType)
        {
            if (String.IsNullOrEmpty (addressString)) {
                return;
            }
            var addressList = NcEmailAddress.ParseAddressListString (addressString);
            foreach (MailboxAddress address in addressList) {
                ProcessAddress (accountId, objectId, address, addressType);
            }
        }

        public override void Run (CancellationToken token)
        {
            // McEmailMessage
            var thisVersion = Version ();
            foreach (var emailMessage in Db.Table<McEmailMessage> ().Where (x => x.MigrationVersion < thisVersion)) {
                token.ThrowIfCancellationRequested ();

                NcModel.Instance.RunInTransaction (() => {
                    var accountId = emailMessage.AccountId;
                    var objectId = emailMessage.Id;

                    emailMessage.FromEmailAddressId = 
                        ProcessAddress (accountId, objectId, emailMessage.From, EmailAddressType.MESSAGE_FROM);
                    emailMessage.SenderEmailAddressId =
                        ProcessAddress (accountId, objectId, emailMessage.Sender, EmailAddressType.MESSAGE_SENDER);
                    ProcessAddressList (accountId, objectId, emailMessage.To, EmailAddressType.MESSAGE_TO);
                    ProcessAddressList (accountId, objectId, emailMessage.Cc, EmailAddressType.MESSAGE_CC);

                    emailMessage.MigrationVersion = thisVersion;
                    emailMessage.Update ();
                    NcMigration.ProcessedObjects += 1;
                });
            }

            // McAttendee
            foreach (var attendee in Db.Table<McAttendee> ().Where (x => x.MigrationVersion < thisVersion)) {
                token.ThrowIfCancellationRequested ();

                NcModel.Instance.RunInTransaction (() => {
                    attendee.EmailAddressId =
                        ProcessAddress (attendee.AccountId, attendee.Id, attendee.Email, EmailAddressType.ATTENDEE_EMAIL);
                    attendee.MigrationVersion = thisVersion;
                    attendee.Update ();
                    NcMigration.ProcessedObjects += 1;
                });
            }

            // McCalendar
            foreach (var cal in Db.Table<McCalendar> ().Where (x => x.MigrationVersion < thisVersion)) {
                token.ThrowIfCancellationRequested ();

                NcModel.Instance.RunInTransaction (() => {
                    cal.OrganizerEmailAddressId =
                        ProcessAddress (cal.AccountId, cal.Id, cal.OrganizerEmail, EmailAddressType.CALENDAR_ORGANIZER);
                    cal.MigrationVersion = thisVersion;
                    cal.Update ();
                    NcMigration.ProcessedObjects += 1;
                });
            }
        }
    }
}

