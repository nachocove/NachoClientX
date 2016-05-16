//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class ActionsHelper
    {
        private DateTime NextUndeferCheckTime;
        private NcTimer UndeferCheckTimer;

        private static ActionsHelper _Instance;
        public static ActionsHelper Instance {
            get {
                if (_Instance == null) {
                    _Instance = new ActionsHelper ();
                }
                return _Instance;
            }
        }

        private ActionsHelper ()
        {
            NextUndeferCheckTime = default(DateTime);
        }

        public void Start ()
        {
            ScheduleNextUndeferCheck ();
        }

        public void Stop ()
        {
            if (UndeferCheckTimer != null) {
                UndeferCheckTimer.Dispose ();
                UndeferCheckTimer = null;
            }
        }

        public void ScheduleNextUndeferCheck ()
        {
            var nextCheckTime = McAction.NextUndeferTime ();
            if (nextCheckTime != default(DateTime)) {
                Log.Info (Log.LOG_BACKEND, "ActionsHelper nextCheckTime = {0}", nextCheckTime);
                NextUndeferCheckTime = nextCheckTime;
                StartDeferCheckTimer ();
            } else {
                Log.Info (Log.LOG_BACKEND, "ActionsHelper not scheduling check", nextCheckTime);
                NextUndeferCheckTime = default(DateTime);
            }
        }

        private void StartDeferCheckTimer ()
        {
            if (UndeferCheckTimer != null) {
                UndeferCheckTimer.Dispose ();
                UndeferCheckTimer = null;
            }
            var span = NextUndeferCheckTime - DateTime.UtcNow;
            var minSpan = TimeSpan.FromSeconds (1);
            if (span < minSpan) {
                span = minSpan;
            }
            UndeferCheckTimer = new NcTimer ("ActionsHelper_DeferCheckTimer", DeferCheckTimerFired, null, span, TimeSpan.Zero);
        }

        void DeferCheckTimerFired (object state)
        {
            UndeferCheckTimer = null;
            NcTask.Run (() => {
                McAction.UndeferActions ();
                ScheduleNextUndeferCheck ();
            }, "ActionsHelper_Undefer", NcTask.ActionSerialScheduler);
        }
    }
}

