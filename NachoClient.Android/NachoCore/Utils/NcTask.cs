//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace NachoCore.Utils
{
    public class NcTask
    {
        static ConcurrentDictionary<WeakReference,string> TaskMap;

        public static void Start ()
        {
            TaskMap = new ConcurrentDictionary<WeakReference, string> ();
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

        public static Task Run (Action action, string name)
        {
            WeakReference taskRef = null;
            var task = new Task (delegate {
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

        public static void Stop ()
        {
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
