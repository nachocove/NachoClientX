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
                emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    if (0 != target.FromEmailAddressId) {
                        fromEmailAddress = McEmailAddress.QueryById<McEmailAddress> (target.FromEmailAddressId);
                    }
                    if (0 == target.cachedFromColor) {
                        if (null == fromEmailAddress) {
                            target.cachedFromColor = 1;
                        } else {
                            target.cachedFromColor = fromEmailAddress.ColorIndex;
                        }
                    }
                    if (String.IsNullOrEmpty (target.cachedFromLetters)) {
                        if (null == fromEmailAddress) {
                            target.cachedFromLetters = "";
                        } else {
                            target.cachedFromLetters = EmailHelper.Initials (target.From);
                        }
                    }
                    return true;
                });
                UpdateProgress (1);
            }
        }
    }
}
