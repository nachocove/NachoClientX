//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoPlatformBinding;
using System.Threading;
using System.Runtime.CompilerServices;
using NachoCore.Model;
using System.Linq;
using System.Collections.Generic;

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

        DateTime? LastStatusIndDetectorReport;

        void DoStatusIndDetectorReport ()
        {
            if (!LastStatusIndDetectorReport.HasValue || LastStatusIndDetectorReport.Value.AddSeconds (5) < DateTime.UtcNow) {
                // do a report on any execution context changes.
                NcTask.Run (() => Report (), "DoStatusIndDetectorReport");
                LastStatusIndDetectorReport = DateTime.UtcNow;
            }
        }

        void StatusIndDetector (object sender, EventArgs ea)
        {
            var siea = (StatusIndEventArgs)ea;
            switch (siea.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ExecutionContextChanged:
                switch ((NcApplication.ExecutionContextEnum)siea.Status.Value) {
                case NcApplication.ExecutionContextEnum.Foreground:
                    DoStatusIndDetectorReport ();
                    Start ();
                    break;

                case NcApplication.ExecutionContextEnum.Background:
                    DoStatusIndDetectorReport ();
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

        DateTime? LastDBRowCounts;

        public void Report (string moniker = null, [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (!String.IsNullOrEmpty (moniker)) {
                Log.Info (Log.LOG_SYS, "NcApplicationMonitor: {0} from line {1} of {2}", moniker, sourceLineNumber, sourceFilePath);
            }
            long processMemory;
            try {
                processMemory = PlatformProcess.GetUsedMemory () / (1024 * 1024);
                ProcessMemory.AddSample ((int)processMemory);
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "NcApplicationMonitor: Could not get PlatformProcess.GetUsedMemory: {0}", ex);
                processMemory = -1;
            }
            Log.Info (Log.LOG_SYS, "NcApplicationMonitor: Memory: Process {0} MB, GC {1} MB",
                processMemory, GC.GetTotalMemory (true) / (1024 * 1024));
            int minWorker, maxWorker, minCompletion, maxCompletion;
            ThreadPool.GetMinThreads (out minWorker, out minCompletion);
            ThreadPool.GetMaxThreads (out maxWorker, out maxCompletion);
            int systemThreads;
            try {
                systemThreads = PlatformProcess.GetNumberOfSystemThreads ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "NcApplicationMonitor: could not get PlatformProcess.GetNumberOfSystemThreads: {0}", ex);
                systemThreads = -1;
            }
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
            if (!LastDBRowCounts.HasValue || LastDBRowCounts.Value.AddHours (4) < DateTime.UtcNow) {
                var counts = NcModel.Instance.AllTableRowCounts ();
                Log.Info (Log.LOG_SYS, "NcApplicationMonitor: DB Row Counts (non-zero):\n{0}", string.Join ("\n", counts.Select (x => string.Format ("{0}: {1}", x.Key, x.Value)).ToList ()));
                LastDBRowCounts = DateTime.UtcNow;
            }
            int maxfd;
            int openfd;
            try {
                maxfd = PlatformProcess.GetCurrentNumberOfFileDescriptors ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "NcApplicationMonitor: could not get PlatformProcess.GetCurrentNumberOfFileDescriptors: {0}", ex);
                maxfd = -1;
            }
            try {
                openfd = PlatformProcess.GetCurrentNumberOfInUseFileDescriptors ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "NcApplicationMonitor: could not get PlatformProcess.GetCurrentNumberOfInUseFileDescriptors: {0}", ex);
                openfd = -1;
            }
            Log.Info (Log.LOG_SYS, "NcApplicationMonitor: Files: Max {0}, Currently open {1}", maxfd, openfd);
            // DumpFileLeaks ();
            if (150 < openfd) {
                DumpFileDescriptors ();
            }
            NcModel.Instance.DumpLastAccess ();
            NcTask.Dump ();

            if (null != MonitorEvent) {
                MonitorEvent (this, EventArgs.Empty);
            }
        }

        public static void DumpFileDescriptors ()
        {
            try {
                var openFds = PlatformProcess.GetCurrentInUseFileDescriptors ();
                Log.Warn (Log.LOG_SYS, "Monitor: FD Dumping current open files {0}", openFds.Length);
                foreach (var fd in openFds) {
                    var path = PlatformProcess.GetFileNameForDescriptor (int.Parse (fd));
                    if (null == path) {
                        continue;
                    }
                    Log.Info (Log.LOG_SYS, "fd {0}: {1}", fd, path);
                }
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "Monitor: Could not dump open files: {0}", ex);
            }
        }

        // FD leak detector

        class FirstSeen
        {
            public bool active;
            public string path;
            public DateTime firstSeen;
        }

        static DateTime firstRun = DateTime.MinValue;
        static Dictionary<string, FirstSeen> FDtracker = new Dictionary<string, FirstSeen> ();

        public static void DumpFileLeaks ()
        {
            try {
                var openFds = PlatformProcess.GetCurrentInUseFileDescriptors ();
                if (DateTime.MinValue == firstRun) {
                    firstRun = DateTime.Now;
                }
                foreach (var e in FDtracker) {
                    e.Value.active = false;
                }
                foreach (var fd in openFds) {
                    var path = PlatformProcess.GetFileNameForDescriptor (int.Parse (fd));
                    FirstSeen firstSeen;
                    if (FDtracker.TryGetValue (fd, out firstSeen)) {
                        firstSeen.active = true;
                        if (path != firstSeen.path) {
                            firstSeen.path = path;
                            firstSeen.firstSeen = DateTime.Now;
                        }
                    } else {
                        var fs = new FirstSeen ();
                        fs.active = true;
                        fs.path = path;
                        fs.firstSeen = DateTime.Now;
                        FDtracker.Add (fd, fs);
                    }
                }
                var toRemove = new List<string> ();
                foreach (var e in FDtracker) {
                    if (!e.Value.active) {
                        toRemove.Add (e.Key);
                    }
                }
                bool printHeader = true;
                foreach (var e in FDtracker) {
                    // Don't report files created when the app starts
                    if (firstRun.AddMinutes (5) > e.Value.firstSeen) {
                        continue;
                    }
                    if (printHeader) {
                        printHeader = false;
                        Log.Warn (Log.LOG_SYS, "Monitor: FD Dumping potential file leaks");
                    }
                    if (DateTime.Now.AddMinutes (-2) > e.Value.firstSeen) {
                        Log.Warn (Log.LOG_SYS, "Monitor: potential leaked FD {0} ({1}): {2}", e.Key, (DateTime.Now - e.Value.firstSeen).TotalSeconds, e.Value.path);
                    }
                }
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "Monitor: Could not DumpFileLeaks: {0}", ex);
            }
        }
    }
}

