// # Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.OS;

namespace NachoPlatform
{
    public class InvokeOnUIThread : IPlatformInvokeOnUIThread
    {
        private static volatile InvokeOnUIThread instance;
        private static object syncRoot = new Object ();

        private InvokeOnUIThread ()
        {
        }

        public static InvokeOnUIThread Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new InvokeOnUIThread ();
                    }
                }
                return instance;
            }
        }

        public void Invoke (Action action)
        {
            using (var handler = new Handler (Looper.MainLooper)) {
                handler.Post (action);
            }
        }
    }
}

