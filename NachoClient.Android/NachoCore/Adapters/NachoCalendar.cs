//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
    public class NachoCalendar : NachoCalendarCommon
    {
        protected override void Reload ()
        {
            list = BackEnd.Instance.Db.Table<McCalendar> ().ToList ();
        }
    }
}
