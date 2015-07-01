//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    // Fill in the UID field in McEvents.

    public class NcMigration31 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McCalendar> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var cal in Db.Table<McCalendar> ()) {
                token.ThrowIfCancellationRequested ();
                Db.Execute ("UPDATE McEvent SET UID = ? WHERE CalendarId = ?", cal.UID, cal.Id);
                UpdateProgress (1);
            }
        }
    }
}
