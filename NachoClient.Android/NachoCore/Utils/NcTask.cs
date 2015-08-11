//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
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
        private static int TaskId = 0;
        public static CancellationTokenSource Cts = new CancellationTokenSource ();
        private static object LockObj = new object ();

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

        public static Task Run (Action action, string name, bool stfu, bool isUnique)
        {
            string dummy = null;
            var taskId = Interlocked.Increment (ref TaskId);
            var taskName = name + taskId.ToString ();
            DateTime spawnTime = DateTime.UtcNow;
            lock (LockObj) {
                if (isUnique) {
                    // Make sure that there is not another task by the same name already running
                    // No reverse mapping just walk. Should be few enough tasks that walking is not a problem.
                    foreach (var pair in TaskMap) {
                        if (pair.Value.StartsWith (name)) {
                            Log.Warn (Log.LOG_SYS, "NcTask {0} already running", pair.Value);
                            return null; // an entry exists
                        }
                    }
                }

                WeakReference taskRef = null;
                var task = Task.Run (delegate {
                    DateTime startTime = DateTime.UtcNow;
                    double latency = (startTime - spawnTime).TotalMilliseconds;
                    if (200 < latency) {
                        Log.Warn (Log.LOG_UTILS, "Delay in running NcTask {0}, latency {1} msec", taskName, latency);
                        NcApplication.Instance.MonitorReport ();
                        NcTask.Dump ();
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
                            Log.Warn (Log.LOG_SYS, "NcTask: closing DB, connections: {0}", count);
                        }
                    }
                    if (!stfu) {
                        Log.Info (Log.LOG_SYS, "NcTask {0} completed.", taskName);
                    }
                }, Cts.Token);
                taskRef = new WeakReference (task);
                if (!TaskMap.TryAdd (taskRef, taskName)) {
                    Log.Error (Log.LOG_SYS, "Task already added to TaskMap ({0}).", taskName);
                }
                return task.ContinueWith (delegate {
                    if (!TaskMap.TryRemove (taskRef, out dummy)) {
                        Log.Error (Log.LOG_SYS, "Task already removed from TaskMap ({0}).", taskName);
                    }
                });
            }
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
