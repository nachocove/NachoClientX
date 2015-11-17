//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;

namespace NachoPlatform
{
    public class Threads : IThreads, IDisposable
    {
        private static volatile Threads instance;
        private static object syncRoot = new Object ();
        private static NSObject token = null;

        public bool DetectsThreadDeath { get { return true; } }

        public event ThreadDeathEventHandler ThreadDeathEvent;

        private Threads ()
        {
            token = NSNotificationCenter.DefaultCenter.AddObserver ((NSString)"NSThreadWillExitNotification", ThisThreadIsDead);
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

        private void ThisThreadIsDead (NSNotification obj)
        {
            if (null != ThreadDeathEvent) {
                ThreadDeathEvent (this, new ThreadDeathEventArgs () {
                    ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                });
            }
        }

        public void Dispose ()
        {
            if (null != token) {
                token.Dispose ();
                token = null;
            }
        }
    }
}

