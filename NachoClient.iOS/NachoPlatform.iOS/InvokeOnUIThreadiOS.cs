// # Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;

namespace NachoPlatform
{
    public class InvokeOnUIThread : NSObject, IPlatformInvokeOnUIThread
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
            this.BeginInvokeOnMainThread (new Action (action));
        }
    }
}