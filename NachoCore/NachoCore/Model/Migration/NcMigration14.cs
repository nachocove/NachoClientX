//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration14 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            Db.Execute ("UPDATE McCalendar SET RecurrencesGeneratedUntil = ? WHERE AllDayEvent = ?", DateTime.MinValue, true);
        }
    }
}

