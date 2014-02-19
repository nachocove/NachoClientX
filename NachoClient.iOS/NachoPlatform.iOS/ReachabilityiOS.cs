// # Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoPlatform
{
    public sealed class Reachability : IPlatformReachability
    {
        private static volatile Reachability instance;
        private static object syncRoot = new Object ();

        private Reachability ()
        {
        }

        public static Reachability Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new Reachability ();
                    }
                }
                return instance;
            }
        }

        public event EventHandler ReachabilityEvent;

        public void AddHost (string host)
        {
        }

        public void RemoveHost (string host)
        {
        }
    }
}