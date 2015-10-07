//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Support.V7.App;

namespace NachoClient.AndroidClient
{
    public class NcAlertView
    {
        /// <summary>
        /// Show an alert view that simply displays a message.  The only button will be "OK",
        /// which will dismiss the view.
        /// </summary>
        public static void ShowMessage (Android.Content.Context context, string title, string message)
        {
            new AlertDialog.Builder (context).SetTitle (title).SetMessage (message).Show ();
        }
    }
}

