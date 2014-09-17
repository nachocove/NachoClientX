//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
    public class NcEventManager : NcEventsCommon
    {
        private static volatile NcEventManager instance;
        private static object syncRoot = new Object ();

        public static NcEventManager Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new NcEventManager ();
                    }
                }
                return instance; 
            }
        }

        protected override void Reload ()
        {
            list = NcModel.Instance.Db.Table<McEvent> ().OrderBy (v => v.StartTime).ToList ();
        }

    }
}

