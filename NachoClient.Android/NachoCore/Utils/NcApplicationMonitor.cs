//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoPlatformBinding;
using System.Threading;
using System.Runtime.CompilerServices;
using NachoCore.Model;

namespace NachoCore
{
    public class NcApplicationMonitor
    {
        public event EventHandler MonitorEvent;

        private NcTimer MonitorTimer;
        private NcSamples ProcessMemory;

        private static volatile NcApplicationMonitor instance;
        private static object syncRoot = new Object ();

        public static NcApplicationMonitor Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new NcApplicationMonitor ();
                        }
                    }
                }
                return instance; 
            }
        }

        public NcApplicationMonitor ()
        {
            ProcessMemory = new NcSamples ("NcApplicationMonitor.ProcessMemory");
            ProcessMemory.MinInput = 0;
            ProcessMemory.MaxInput = 100000;
            ProcessMemory.LimitInput = true;
            ProcessMemory.ReportThreshold = 4;
        }

        void StatusIndDetector (object sender, EventArgs ea)
        {
            var siea = (StatusIndEventArgs)ea;
            switch (siea.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ExecutionContextChanged:
                switch ((NcApplication.ExecutionContextEnum)siea.Status.Value) {
                case NcApplication.ExecutionContextEnum.Foreground:
                    Start ();
                    break;

                case NcApplication.ExecutionContextEnum.Background:
                    Start (MonitorTimerDefaultDueSecs * MonitorTimerDefaultBGMultiplier,
                        MonitorTimerDefaultPeriodicSecs * MonitorTimerDefaultBGMultiplier);
                    break;

                default:
                    break;
                }
                break;
            }
        }

        const int MonitorTimerDefaultDueSecs = 30;
        const int MonitorTimerDefaultPeriodicSecs = 60;
        const int MonitorTimerDefaultBGMultiplier = 4;

        public void Start (int timerWaitSecs = MonitorTimerDefaultDueSecs, int timerPeriodicSecs = MonitorTimerDefaultPeriodicSecs)
        {
            var due = new TimeSpan (0, 0, timerWaitSecs);
            var period = new TimeSpan (0, 0, timerPeriodicSecs);

            if (null == MonitorTimer) {
                NcApplication.Instance.StatusIndEvent += StatusIndDetector;

                MonitorTimer = new NcTimer ("NcApplicationMonitor", (state) => Report (),
                    null, due, period);
                MonitorTimer.Stfu = true;
            } else {
                MonitorTimer.Change (due, period);
            }
        }

        public void Stop ()
        {
            if (null != MonitorTimer) {
                MonitorTimer.Dispose ();
                MonitorTimer = null;
                NcApplication.Instance.StatusIndEvent -= StatusIndDetector;
            }
        }

        public void Report (string moniker = null, [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (!String.IsNullOrEmpty (moniker)) {
                Log.Info (Log.LOG_SYS, "NcApplicationMonitor: {0} from line {1} of {2}", moniker, sourceLineNumber, sourceFilePath);
            }
            var processMemory = PlatformProcess.GetUsedMemory () / (1024 * 1024);
            ProcessMemory.AddSample ((int)processMemory);
            Log.Info (Log.LOG_SYS, "NcApplicationMonitor: Memory: Process {0} MB, GC {1} MB",
                processMemory, GC.GetTotalMemory (true) / (1024 * 1024));
            int minWorker, maxWorker, minCompletion, maxCompletion;
            ThreadPool.GetMinThreads (out minWorker, out minCompletion);
            ThreadPool.GetMaxThreads (out maxWorker, out maxCompletion);
            int systemThreads = PlatformProcess.GetNumberOfSystemThreads ();
            string message = string.Format ("NcApplicationMonitor: Threads: Min {0}/{1}, Max {2}/{3}, System {4}",
                                 minWorker, minCompletion, maxWorker, maxCompletion, systemThreads);
            if (50 > systemThreads) {
                Log.Info (Log.LOG_SYS, message);
            } else {
                Log.Warn (Log.LOG_SYS, message);
            }
            Log.Info (Log.LOG_SYS, "NcApplicationMonitor: Status: Comm {0}, Speed {1}, Battery {2:00}% {3}",
                NcCommStatus.Instance.Status, NcCommStatus.Instance.Speed,
                NachoPlatform.Power.Instance.BatteryLevel * 100.0, NachoPlatform.Power.Instance.PowerState);
            Log.Info (Log.LOG_SYS, "NcApplicationMonitor: DB Connections {0}", NcModel.Instance.NumberDbConnections);
            Log.Info (Log.LOG_SYS, "NcApplicationMonitor: Files: Max {0}, Currently open {1}",
                PlatformProcess.GetCurrentNumberOfFileDescriptors (), PlatformProcess.GetCurrentNumberOfInUseFileDescriptors ());
            if (100 < PlatformProcess.GetCurrentNumberOfInUseFileDescriptors ()) {
                Log.DumpFileDescriptors ();
            }
            NcModel.Instance.DumpLastAccess ();
            NcTask.Dump ();

            if (null != MonitorEvent) {
                MonitorEvent (this, EventArgs.Empty);
            }
        }
    }
}

