//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoPlatform
{
    public class Threads : IThreads, IDisposable
    {
        private static volatile Threads instance;
        private static object syncRoot = new Object ();

        public bool DetectsThreadDeath { get { return false; } }

        public event ThreadDeathEventHandler ThreadDeathEvent;

        private Threads ()
        {
        }

        public static Threads Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new Threads ();
                    }
                }
                return instance;
            }
        }
    }
}

