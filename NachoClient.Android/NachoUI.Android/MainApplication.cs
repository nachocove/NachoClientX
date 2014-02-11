using System;
using Android.App;
using Android.Runtime;

namespace NachoClient.AndroidClient
{
    [Application]
    public class MainApplication : Application
    {
        public MainApplication (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer)
        {
        }

        public override void OnCreate ()
        {
            base.OnCreate ();
        }
    }
}