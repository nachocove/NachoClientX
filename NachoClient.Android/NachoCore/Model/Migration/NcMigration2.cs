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
            var thisVersion = Version ();
            return (
                Db.Table<McEmailMessage> ().Where (x => x.MigrationVersion < thisVersion).Count ()
                + Db.Table<McAttendee> ().Where (x => x.MigrationVersion < thisVersion).Count ()
                + Db.Table<McCalendar> ().Where (x => x.MigrationVersion < thisVersion).Count ()
            );
        }

        public int ProcessAddress (int accountId, int objectId, MailboxAddress address, NcEmailAddress.Kind addressType)
        {
            McEmailAddress emailAddress;
            var got = McEmailAddress.Get (accountId, address.Address, out emailAddress);
            NcAssert.True (got);

            var map = new McMapEmailAddressEntry ();
            map.ObjectId = objectId;
            map.AccountId = accountId;
            map.EmailAddressId = emailAddress.Id;
            map.AddressType = addressType;
            // Override the default current version as there may be migration between this and current version
            map.MigrationVersion = Version ();
            map.Insert ();

            return emailAddress.Id;
        }

        public int ProcessAddress (int accountId, int objectId, string addressString, NcEmailAddress.Kind addressType)
        {
            if (String.IsNullOrEmpty (addressString)) {
                return 0;
            }
            var address = NcEmailAddress.ParseMailboxAddressString (addressString);
            if (null == address) {
                return 0;
            }
            return ProcessAddress (accountId, objectId, address, addressType);
        }

        public void ProcessAddressList (int accountId, int objectId, string addressString, NcEmailAddress.Kind addressType)
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
            foreach (McEmailMessage emailMessage in Db.Table<McEmailMessage> ().Where (x => x.MigrationVersion < thisVersion)) {
                token.ThrowIfCancellationRequested ();

                NcModel.Instance.RunInTransaction (() => {
                    var accountId = emailMessage.AccountId;
                    var objectId = emailMessage.Id;

                    emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                        var target = (McEmailMessage)record;
                        target.FromEmailAddressId = 
                            ProcessAddress (accountId, objectId, target.From, NcEmailAddress.Kind.From);
                        target.SenderEmailAddressId =
                            ProcessAddress (accountId, objectId, target.Sender, NcEmailAddress.Kind.Sender);
                        ProcessAddressList (accountId, objectId, target.To, NcEmailAddress.Kind.To);
                        ProcessAddressList (accountId, objectId, target.Cc, NcEmailAddress.Kind.Cc);
                        target.MigrationVersion = thisVersion;
                        return true;
                    });
                    UpdateProgress (1);
                });
            }

            // McAttendee
            foreach (McAttendee attendee in Db.Table<McAttendee> ().Where (x => x.MigrationVersion < thisVersion)) {
                token.ThrowIfCancellationRequested ();

                NcModel.Instance.RunInTransaction (() => {
                    attendee.EmailAddressId =
                        ProcessAddress (attendee.AccountId, attendee.Id, attendee.Email,
                        NcEmailAddress.ToKind (attendee.AttendeeType));
                    attendee.MigrationVersion = thisVersion;
                    attendee.Update ();
                    UpdateProgress (1);
                });
            }

            // McCalendar
            foreach (McCalendar cal in Db.Table<McCalendar> ().Where (x => x.MigrationVersion < thisVersion)) {
                token.ThrowIfCancellationRequested ();

                NcModel.Instance.RunInTransaction (() => {
                    cal.OrganizerEmailAddressId =
                        ProcessAddress (cal.AccountId, cal.Id, cal.OrganizerEmail, NcEmailAddress.Kind.Organizer);
                    cal.MigrationVersion = thisVersion;
                    cal.Update ();
                    UpdateProgress (1);
                });
            }
        }
    }
}

