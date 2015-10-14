//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Support.V7.App;
using NachoCore.Utils;

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
            Telemetry.RecordUiAlertView (title, message);
            new AlertDialog.Builder (context).SetTitle (title).SetMessage (message).Show ();
        }

        public static void Show (Android.Content.Context context, string title, string message, Action action)
        {
            Telemetry.RecordUiAlertView (title, message);
            var alert = new AlertDialog.Builder (context).SetTitle (title).SetMessage (message);
            alert.SetPositiveButton ("OK", (dialog, which) => {
                action ();
            });
            alert.Show ();
        }

    }
}

