//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    /// <summary>
    /// A wrapper for an alert view.  It uses UIAlertView on iOS 7, and UIActionController in alert mode on iOS 8.
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
            if (UIDevice.CurrentDevice.CheckSystemVersion (8, 0)) {

                // iOS 8 or higher.  Use UIAlertController.
                var alertController = UIAlertController.Create (title, message, UIAlertControllerStyle.Alert);
                foreach (var action in actions) {
                    alertController.AddAction (UIAlertAction.Create (action.Title, action.UIStyle (), (UIAlertAction obj) => {
                        Telemetry.RecordUiAlertView (title, action.Title);
                        if (null != action.Action) {
                            action.Action ();
                        }
                    }));
                }
                parentViewController.PresentViewController (alertController, true, null);

            } else {

                // iOS 7.  Use UIAlertView.
                var alert = new UIAlertView ();
                alert.Title = title;
                alert.Message = message;
                for (int i = 0; i < actions.Length; ++i) {
                    alert.AddButton (actions [i].Title);
                    if (NcAlertActionStyle.Cancel == actions [i].Style) {
                        alert.CancelButtonIndex = i;
                    }
                }
                EventHandler<UIButtonEventArgs> clickedAction = null;
                clickedAction = (object sender, UIButtonEventArgs e) => {
                    NcAssert.True (0 <= e.ButtonIndex && e.ButtonIndex < actions.Length,
                        string.Format ("The index of the alert button that was clicked ({0}) is outside of the expected range (0..{1})",
                            e.ButtonIndex, actions.Length - 1));
                    Telemetry.RecordUiAlertView (title, actions [e.ButtonIndex].Title);
                    if (null != actions [e.ButtonIndex].Action) {
                        actions [e.ButtonIndex].Action ();
                    }
                    alert.Clicked -= clickedAction;
                };
                alert.Clicked += clickedAction;
                alert.Show ();
            }
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
