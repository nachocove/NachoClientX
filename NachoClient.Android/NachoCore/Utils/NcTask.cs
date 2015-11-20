//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class NcTask
    {
        public const int MaxCancellationTestInterval = 100;
        private static ConcurrentDictionary<WeakReference,string> TaskMap;
        private static ConcurrentDictionary<string,string> UniqueList;
        private static int TaskId = 0;
        public static CancellationTokenSource Cts = new CancellationTokenSource ();

        public static int TaskCount {
            get {
                return TaskMap.Count;
            }
        }

        public static void StartService ()
        {
            if (null == TaskMap) {
                TaskMap = new ConcurrentDictionary<WeakReference, string> ();
            }
            if (null == UniqueList) {
                UniqueList = new ConcurrentDictionary<string, string> ();
            }
            Dump (true);
            Cts = new CancellationTokenSource ();
        }

        public static List<string> FindFaulted ()
        {
            var faulted = new List<string> ();
            foreach (var pair in TaskMap) {
                try {
                    var taskRef = pair.Key;
                    if (taskRef.IsAlive) {
                        if (((Task)taskRef.Target).IsFaulted) {
                            faulted.Add (pair.Value);
                        }
                    }
                } catch {
                    // tasks may be going away as we iterate.
                }
            }
            return faulted;
        }

        public static Task Run (Action action, string name)
        {
            return Run (action, name, false, false);
        }

        public static Task Run (Action action, string name, bool stfu)
        {
            return Run (action, name, stfu, false);
        }

        static string[] LoginRunningTasks = new string[] {
            "Brain",
            "CheckNotified",
            "DeviceProtoControl:DeviceDbChange",
            "DeviceProtoControl:DoSync",
            "DnldEmailBodyCmd",
            "ExpandRecurrences",
            "ImapCredResp",
            "ImapDiscoverCommand",
            "ImapEmailMarkReadCommand",
            "ImapEmailMoveCommand",
            "ImapFetchAttachmentCommand",
            "ImapFetchBodyCommand",
            "ImapFolderSyncCommand",
            "ImapIdleCommand",
            "ImapSyncCommand",
            "InitializeJunkFolders",
            "MarkEmailReadCmd",
            "NcAppStartup",
            "NcEventsCalendarMapCommonRefresh",
            "PushAssistDeferSession",
            "PushAssistDeviceToken",
            "PushAssistStartSession",
            "SafeMode",
            "ScheduleNotifications",
            "SmtpAuthenticateCommand",
            "SmtpCredResp",
            "SmtpDisconnectCommand",
            "SmtpDiscoveryCommand",
            "SmtpReplyMailCommand",
            "Start",
            "SyncCmd",
            "Telemetry",
            "UpdateUnreadMessageCount",
            "UpdateUnreadMessageView",
        };

        struct tracer
        {
            public int parent;
            public int child;
        }

        public static Task Run (Action action, string name, bool stfu, bool isUnique, TaskCreationOptions option = TaskCreationOptions.None)
        {
            string dummy = null;
            var taskId = Interlocked.Increment (ref TaskId);
            var taskName = name + taskId.ToString ();
            DateTime spawnTime = DateTime.UtcNow;

            if (isUnique && !UniqueList.TryAdd (name, taskName)) {
                string runningTaskName;
                if (UniqueList.TryGetValue (name, out runningTaskName)) {
                    Log.Warn (Log.LOG_SYS, "NcTask {0} already running {1}", name, runningTaskName);
                } else {
                    Log.Warn (Log.LOG_SYS, "NcTask {0} running instance has disappeared", name);
                }
                return null; // an entry exists
            }

            if (LoginRunningTasks.Contains (name)) {
                Log.Info (Log.LOG_SYS, "NcTask {0} will be long running", name);
                option = TaskCreationOptions.LongRunning;
            }

            TaskFactory factory;

            #if __ANDROID__
            var proxyTaskScheduler = new SimpleTaskScheduler ();
            factory = new TaskFactory (proxyTaskScheduler);
            #else
            factory = new TaskFactory (TaskScheduler.Current);
            #endif

            var tracer = new tracer ();
            tracer.parent = 0;
            tracer.child = 0;

            var spawningId = Thread.CurrentThread.ManagedThreadId;
            var task = factory.StartNew (delegate {
                #if __ANDROID__
                if (Android.OS.Looper.MyLooper () == Android.OS.Looper.MainLooper) {
                    Log.Error (Log.LOG_UTILS, "StartNew running on main thread.");
                }
                #endif
                DateTime startTime = DateTime.UtcNow;
                double latency = (startTime - spawnTime).TotalMilliseconds;
                if (200 < latency) {
                    Log.Warn (Log.LOG_UTILS, "NcTask: Delay in running NcTask {0}, latency {1:n0} msec", taskName, latency);
                }
                if (Thread.CurrentThread.ManagedThreadId == spawningId) {
                    Interlocked.Increment (ref tracer.child);
                    Log.Warn (Log.LOG_UTILS, "NcTask {0} running on spawning thread (parent={1})", taskName, tracer.parent);
                }
                if (!stfu) {
                    Log.Info (Log.LOG_SYS, "NcTask {0} started, {1} running", taskName, TaskMap.Count);
                }
                try {
                    action.Invoke ();
                } catch (OperationCanceledException) {
                    Log.Info (Log.LOG_SYS, "NcTask {0} cancelled.", taskName);
                } finally {
                    var count = NcModel.Instance.NumberDbConnections;
                    if (15 < count) {
                        NcModel.Instance.Db = null;
                        Log.Info (Log.LOG_SYS, "NcTask closing DB, connections: {0}", count);
                    }
                }
                if (!stfu) {
                    var finishTime = DateTime.UtcNow;
                    if (Thread.CurrentThread.ManagedThreadId == spawningId) {
                        Log.Info (Log.LOG_SYS, "NcTask {0} completed after {1:n0} msec on spawning thread.", taskName, (finishTime - spawnTime).TotalMilliseconds);
                    } else {
                        Log.Info (Log.LOG_SYS, "NcTask {0} completed after {1:n0} msec.", taskName, (finishTime - startTime).TotalMilliseconds);
                    }
                }
            }, Cts.Token, option);
            var taskRef = new WeakReference (task);
            if (!TaskMap.TryAdd (taskRef, taskName)) {
                Log.Error (Log.LOG_SYS, "NcTask: Task already added to TaskMap ({0}).", taskName);
            }
            Interlocked.Increment (ref tracer.parent);
            if (0 != tracer.child) {
                Log.Error (Log.LOG_SYS, "NcTask: Spawned task {0} is running", taskName);
            }
            return task.ContinueWith (delegate {
                if (!TaskMap.TryRemove (taskRef, out dummy)) {
                    Log.Error (Log.LOG_SYS, "NcTask: Task already removed from TaskMap ({0}).", taskName);
                }
                if (isUnique && !UniqueList.TryRemove (name, out dummy)) {
                    Log.Error (Log.LOG_SYS, "NcTask: Task already removed from UniqueList ({0}).", taskName);
                }
            });

        }

        public static void StopService ()
        {
            Log.Info (Log.LOG_SYS, "NcTask: Stopping all NCTasks...");
            Cts.Cancel ();
            Task.WaitAny (new Task[] { Task.Delay (4 * MaxCancellationTestInterval) });
            foreach (var pair in TaskMap) {
                try {
                    var taskRef = pair.Key;
                    if (taskRef.IsAlive) {
                        if (!((Task)taskRef.Target).IsCompleted) {
                            Log.Warn (Log.LOG_SYS, "NcTask: Task {0} still running", pair.Value);
                        }
                        if (((Task)taskRef.Target).IsCanceled) {
                            Log.Info (Log.LOG_SYS, "NcTask: Task {0} cancelled", pair.Value);
                        }
                    }
                } catch (Exception e) {
                    // tasks may be going away as we iterate.
                    Log.Info (Log.LOG_SYS, "NcTask: Error stopping NcTask {0}", e.Message);
                }
            }
            Log.Info (Log.LOG_SYS, "NcTask: Stopped all NCTasks.");
        }

        public static void Dump (bool warnLivedTasks = false)
        {
            if (null == TaskMap) {
                return;
            }
            foreach (var pair in TaskMap) {
                try {
                    var taskName = pair.Value;
                    var taskRef = pair.Key;
                    if (!taskRef.IsAlive) {
                        Log.Info (Log.LOG_SYS, "Task {0} is not alive", taskName);
                        continue;
                    }
                    var task = (Task)taskRef.Target;
                    if (!task.IsCompleted && warnLivedTasks) {
                        Log.Error (Log.LOG_SYS, "Task {0} survives across shutdown", pair.Value);
                    }
                    Log.Info (Log.LOG_SYS, "Task {0}: IsCompleted={1}, IsCanceled={2}, IsFaulted={3}, status={4}",
                        taskName, task.IsCompleted, task.IsCanceled, task.IsFaulted, task.Status);
                } catch {
                    // tasks may be going away as we iterate.
                }
            }
        }

        /// <summary>
        /// Sleep for a number of milliseconds 
        /// </summary>
        /// <returns><c>true</c> if it sleeps for the specified duration. If the sleep is interrupted by
        /// cancellation, return <c>false</c>.</returns>
        /// <param name="msec">Msec.</param>
        /// <param name="token">Token.</param>
        public static bool CancelableSleep (int msec, CancellationToken token)
        {
            try {
                Task.WaitAll (new Task[] { Task.Delay (msec, token) });
                return true;
            } catch (AggregateException e) {
                foreach (var ex in e.InnerExceptions) {
                    if (ex is OperationCanceledException) {
                        continue;
                    }
                    throw;
                }
                return false;
            }
        }

        public static bool CancelableSleep (int msec)
        {
            return CancelableSleep (msec, Cts.Token);
        }
    }
}
