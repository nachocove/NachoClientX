//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NachoCore.Utils
{
    public class NcTask : Task
    {
        const int MaxCancellationTestInterval = 250;
        static private ConcurrentDictionary<WeakReference,string> TaskMap;
        static private CancellationTokenSource Cts = new CancellationTokenSource ();

        public CancellationToken Token { set; get; }
        public int PreferredCancellationTestInterval { set; get; }

        private NcTask (Action action) : base (action, Cts.Token)
        {
            Token = Cts.Token;
            PreferredCancellationTestInterval = MaxCancellationTestInterval;
        }

        public static void StartService ()
        {
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
                            faulted.Add(pair.Value);
                        }
                    }
                } catch {
                    // tasks may be going away as we iterate.
                }
            }
            return faulted;
        }

        public static NcTask Run (Action action, string name)
        {
            WeakReference taskRef = null;
            var task = new NcTask (delegate {
                action.Invoke ();
                Log.Info (Log.LOG_SYS, "NcTask {0} completed.", name);
                if (!TaskMap.TryRemove (taskRef, out name)) {
                    Log.Error (Log.LOG_SYS, "Task {0} already removed from TaskMap.", name);
                }
            });
            taskRef = new WeakReference (task);
            if (!TaskMap.TryAdd (taskRef, name)) {
                Log.Error (Log.LOG_SYS, "Task {0} already removed from TaskMap.", name);
            }
            task.Start ();
            Log.Info (Log.LOG_SYS, "Task {0} started.", name);
            return task;
        }

        public static void StopService ()
        {
            Cts.Cancel ();
            foreach (var pair in TaskMap) {
                try {
                    var taskRef = pair.Key;
                    if (taskRef.IsAlive) {
                        if (!((Task)taskRef.Target).IsCompleted) {
                            Log.Error (Log.LOG_SYS, "Task {0} still running", pair.Value);
                        }
                    }
                } catch {
                    // tasks may be going away as we iterate.
                }
            }
        }
    }
}
