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
    public class NcMigration6 : NcMigration
    {
        public NcMigration6 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return Db.Table<McEmailMessage> ().Where (x => x.ConversationId == null).Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var emailMessage in Db.Table<McEmailMessage>().Where (x => x.ConversationId == null)) {
                token.ThrowIfCancellationRequested ();
                NcModel.Instance.RunInTransaction (() => {
                    emailMessage.ConversationId = System.Guid.NewGuid ().ToString ();
                    emailMessage.Update();
                    UpdateProgress (1);
                });
            }
        }
    }
}

