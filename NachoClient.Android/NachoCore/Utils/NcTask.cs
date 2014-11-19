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

        public static void StartService ()
        {
            if (null != TaskMap) {
                foreach (var pair in TaskMap) {
                    try {
                        var taskRef = pair.Key;
                        if (!taskRef.IsAlive) {
                            continue;
                        }
                        if (!((Task)taskRef.Target).IsCompleted) {
                            Log.Error (Log.LOG_SYS, "Task {0} survives across shutdown", pair.Value);
                        }
                    } catch {
                        // tasks may be going away as we iterate.
                    }
                }
            }
            TaskMap = new ConcurrentDictionary<WeakReference, string> ();
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
            return Run (action, name, false);
        }

        public static Task Run (Action action, string name, bool stfu)
        {
            string dummy = null;
            var taskId = Interlocked.Increment (ref TaskId);
            var taskName = name + taskId.ToString ();
            WeakReference taskRef = null;
            var task = Task.Run (delegate {
                if (!stfu) {
                    Log.Info (Log.LOG_SYS, "NcTask {0} started.", taskName);
                }
                action.Invoke ();
                if (!stfu) {
                    Log.Info (Log.LOG_SYS, "NcTask {0} completed.", taskName);
                }
                if (null == taskRef) {
                    // XAMMIT - Likely inappropriate Task inlining.
                    Log.Warn (Log.LOG_SYS, "NcTask {0}: Weak reference unavailable", taskName);
                }
                else if (!TaskMap.TryRemove (taskRef, out dummy)) {
                    Log.Error (Log.LOG_SYS, "Task already removed from TaskMap ({0}).", taskName);
                }
            }, Cts.Token);
            taskRef = new WeakReference (task);
            if (!TaskMap.TryAdd (taskRef, taskName)) {
                Log.Error (Log.LOG_SYS, "Task already added to TaskMap ({0}).", taskName);
            }
            return task;
            // XAMMIT - Task.Start() does not work reliably (FYI).
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
                        if (!((Task)taskRef.Target).IsCanceled) {
                            Log.Info (Log.LOG_SYS, "Task {0} cancelled", pair.Value);
                        }
                    }
                } catch {
                    // tasks may be going away as we iterate.
                }
            }
        }
    }
}
