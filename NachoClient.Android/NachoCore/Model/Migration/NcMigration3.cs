//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;

namespace NachoCore.Model
{
    public class NcMigration3 : NcMigration
    {
        public NcMigration3 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return Db.Table<McContact> ().Count ();
        }

        public override void Run (CancellationToken token)
        {
            var thisVersion = Version ();
            foreach (McContact contact in Db.Table<McContact> ().Where (x => x.MigrationVersion < thisVersion)) {
                token.ThrowIfCancellationRequested ();

                NcModel.Instance.RunInTransaction (() => {
                    var newEmailAddressEclipsed = contact.ShouldEmailAddressesBeEclipsed ();
                    var newPhoneNumbersEclipsed = contact.ShouldPhoneNumbersBeEclipsed ();
                    if ((newEmailAddressEclipsed != contact.EmailAddressesEclipsed) ||
                        (newPhoneNumbersEclipsed != contact.PhoneNumbersEclipsed)) {
                        contact.EmailAddressesEclipsed = newEmailAddressEclipsed;
                        contact.PhoneNumbersEclipsed = newPhoneNumbersEclipsed;
                        contact.Update ();
                    }
                    UpdateProgress (1);
                });
            }
        }
    }
}

