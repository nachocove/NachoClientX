//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.iOS
{
    /// <summary>
    /// A wrapper for an alert view, which is UIAlertController in alert mode.
    /// Text fields in the alert view are not yet supported.
    /// </summary>
    public class NcAlertView
    {
        /// <summary>
        /// Show an alert view that has custom actions.
        /// </summary>
        public static void Show (
            UIViewController parentViewController, string title, string message, params NcAlertAction[] actions)
        {
            var alertController = UIAlertController.Create (title, message, UIAlertControllerStyle.Alert);
            foreach (var action in actions) {
                alertController.AddAction (UIAlertAction.Create (action.Title, action.UIStyle (), (UIAlertAction obj) => {
                    NcApplication.Instance.TelemetryService.RecordUiAlertView (title, action.Title);
                    if (null != action.Action) {
                        action.Action ();
                    }
                }));
            }
            parentViewController.PresentViewController (alertController, true, null);
        }

        /// <summary>
        /// Show an alert view that simply displays a message.  The only button will be "OK",
        /// which will dismiss the view.
        /// </summary>
        public static void ShowMessage (UIViewController parentViewController, string title, string message)
        {
            Show (parentViewController, title, message, new NcAlertAction ("OK", NcAlertActionStyle.Cancel, null));
        }
    }
}
