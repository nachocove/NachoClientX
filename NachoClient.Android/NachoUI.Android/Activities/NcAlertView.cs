//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Support.V7.App;
using NachoCore.Utils;
using Android.Content;
using NachoCore;

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
            NcApplication.Instance.TelemetryService.RecordUiAlertView (title, message);
            new AlertDialog.Builder (context).SetTitle (title).SetMessage (message).Show ();
        }

        public static void Show (Android.Content.Context context, string title, string message, Action action)
        {
            NcApplication.Instance.TelemetryService.RecordUiAlertView (title, message);
            var builder = new AlertDialog.Builder (context).SetTitle (title).SetMessage (message);
            builder.SetPositiveButton ("OK", (dialog, which) => {
                action ();
            });
            var alert = builder.Create();
            alert.CancelEvent += (object sender, EventArgs e) => {
                action ();
            };
            alert.Show ();
        }

        public static void Show (Android.Content.Context context, string title, string message, Action ok_action, Action cancel_action)
        {
            NcApplication.Instance.TelemetryService.RecordUiAlertView (title, message);
            var builder = new AlertDialog.Builder (context).SetTitle (title).SetMessage (message);
            builder.SetPositiveButton ("OK", (dialog, which) => {
                ok_action ();
            });
            builder.SetNegativeButton ("Cancel", (dialog, which) => {
                cancel_action ();
            });
            var alert = builder.Create ();
            alert.CancelEvent += (object sender, EventArgs e) => {
                cancel_action ();
            };
            alert.Show ();
        }

    }
}

