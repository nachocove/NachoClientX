//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration43 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return 3;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            Db.Execute ("UPDATE McCalendar SET SerializedAttendeeList = 'OldStyle'");
            UpdateProgress (1);
            Db.Execute ("UPDATE McException SET SerializedAttendeeList = 'OldStyle'");
            UpdateProgress (1);
            Db.Execute ("UPDATE McMeetingRequest SET SerializedAttendeeList = 'OldStyle'");
            UpdateProgress (1);
        }
    }
}

