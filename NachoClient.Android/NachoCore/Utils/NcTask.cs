//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NachoCore.Utils
{
    public class NcTask
    {
        public const int MaxCancellationTestInterval = 100;
        private static ConcurrentDictionary<WeakReference,string> TaskMap;
        private static int TaskId = 0;
        public static CancellationTokenSource Cts = new CancellationTokenSource ();
        private static object LockObj = new object ();

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
            lock (LockObj) {
                if (isUnique) {
                    // Make sure that there is not another task by the same name already running
                    // No reverse mapping just walk. Should be few enough tasks that walking is not a problem.
                    foreach (var pair in TaskMap) {
                        var taksname = pair.Value;
                        if (taskName == name) {
                            Log.Info (Log.LOG_SYS, "NcTask {0} already running", taskName);
                            return null; // an entry exists
                        }
                    }
                }

                WeakReference taskRef = null;
                var task = Task.Run (delegate {
                    if (!stfu) {
                        Log.Info (Log.LOG_SYS, "NcTask {0} started.", taskName);
                    }
                    try {
                        action.Invoke ();
                    } catch (OperationCanceledException) {
                        Log.Info (Log.LOG_SYS, "NcTask {0} cancelled.", taskName);
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
            Cts.Cancel ();
            Task.WaitAny (new Task[] { Task.Delay (4 * MaxCancellationTestInterval) });
            foreach (var pair in TaskMap) {
                try {
                    var taskRef = pair.Key;
                    if (taskRef.IsAlive) {
                        if (!((Task)taskRef.Target).IsCompleted) {
                            Log.Warn (Log.LOG_SYS, "Task {0} still running", pair.Value);
                        }
                        if (((Task)taskRef.Target).IsCanceled) {
                            Log.Info (Log.LOG_SYS, "Task {0} cancelled", pair.Value);
                        }
                    }
                } catch {
                    // tasks may be going away as we iterate.
                }
            }
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
                    Log.Info (Log.LOG_SYS, "Task {0}: IsCompleted={0}, IsCanceled={1}, IsFaulted={2}",
                        taskName, task.IsCompleted, task.IsCanceled, task.IsFaulted);
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
