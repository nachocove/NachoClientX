//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoPlatform
{
    public sealed class Calendars : IPlatformCalendars
    {
        private const int SchemaRev = 0;
        private static volatile Calendars instance;
        private static object syncRoot = new Object ();

        private Calendars ()
        {
        }

        public static Calendars Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new Calendars ();
                        }
                    }
                }
                return instance;
            }
        }

        public class PlatformCalendarRecordAndroid : PlatformCalendarRecord
        {
            public override string ServerId { get { return null; } }

            public override DateTime LastUpdate { get { return DateTime.MaxValue; } } // FIXME.


            public override NcResult ToMcCalendar ()
            {
                return null;
            }

        }
        public void AskForPermission (Action<bool> result)
        {
        }

        public IEnumerable<PlatformCalendarRecord> GetCalendars ()
        {
            return null;
        }

        public event EventHandler ChangeIndicator;

        public NcResult Add (McCalendar contact)
        {
            return NcResult.Error ("Android Calendars.Add not yet implemented.");
        }

        public NcResult Delete (string serverId)
        {
            return NcResult.Error ("Android Calendars.Delete not yet implemented.");
        }

        public NcResult Change (McCalendar contact)
        {
            return NcResult.Error ("Android Calendars.Change not yet implemented.");
        }

        public bool AuthorizationStatus {
            get {
                // TODO Not yet implemented.
                return false;
            }
        }
    }
}

