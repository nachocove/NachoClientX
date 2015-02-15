//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    /// <summary>
    /// Migrate the McAttendee.AttendeeType and McAttendee.AttendeeTypeIsSet fields.  In the past, they could have the
    /// values of Unknown and false if the user was connected to a Google server.  The ActiveSync code has been changed
    /// to set the AttendeeType to Required is none is specified, but old McAttendee items need to be migrated.
    /// </summary>
    public class NcMigration4 : NcMigration
    {
        public NcMigration4 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return Db.Table<McAttendee> ().Where (x =>
                x.AttendeeTypeIsSet == false || x.AttendeeType == NcAttendeeType.Unknown).Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (McAttendee attendee in Db.Table<McAttendee>().Where (x => x.AttendeeTypeIsSet == false || x.AttendeeType ==  NcAttendeeType.Unknown)) {
                token.ThrowIfCancellationRequested ();
                NcModel.Instance.RunInTransaction (() => {
                    attendee.AttendeeTypeIsSet = true;
                    if (NcAttendeeType.Unknown == attendee.AttendeeType) {
                        attendee.AttendeeType = NcAttendeeType.Required;
                    }
                    attendee.Update ();
                    UpdateProgress (1);
                });
            }
        }
    }
}

