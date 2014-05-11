//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
    public class NcCalendarManager : NachoCalendarCommon
    {
        private static volatile NcCalendarManager instance;
        private static object syncRoot = new Object ();

        public static NcCalendarManager Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new NcCalendarManager ();
                    }
                }
                return instance; 
            }
        }

        protected override void Reload ()
        {
            list = NcModel.Instance.Db.Table<McCalendar> ().ToList ();
        }

    }
}

