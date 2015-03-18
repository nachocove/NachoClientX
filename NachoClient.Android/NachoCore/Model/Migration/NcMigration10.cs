//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class NcMigration10 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McEmailMessage> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var emailMessage in Db.Table<McEmailMessage> ()) {
                token.ThrowIfCancellationRequested ();
                McEmailAddress fromEmailAddress = null;
                if (0 != emailMessage.FromEmailAddressId) {
                    fromEmailAddress = McEmailAddress.QueryById<McEmailAddress> (emailMessage.FromEmailAddressId);
                }
                if (0 == emailMessage.cachedFromColor) {
                    if (null == fromEmailAddress) {
                        emailMessage.cachedFromColor = 1;
                    } else {
                        emailMessage.cachedFromColor = fromEmailAddress.ColorIndex;
                    }
                }
                if (String.IsNullOrEmpty (emailMessage.cachedFromLetters)) {
                    if (null == fromEmailAddress) {
                        emailMessage.cachedFromLetters = "";
                    } else {
                        emailMessage.cachedFromLetters = EmailHelper.Initials (emailMessage.From);
                    }
                }
                emailMessage.Update ();
                UpdateProgress (1);
            }
        }
    }
}
