//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    /// <summary>
    /// A wrapper for an action sheet.  It uses UIActionSheet on iOS 7, and UIActionController in action sheet mode
    /// on iOS 8.
    /// </summary>
    public class NcActionSheet
    {
        /// <summary>
        /// Show an action sheet with custom actions.  On iOS 8, inclde a title and message.
        /// </summary>
        public static void Show (
            UIView parentView, UIViewController parentViewController, string title, string message, params NcAlertAction[] actions)
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion (8, 0)) {

                // iOS 8 or higher.  Use UIAlertController.
                var alertController = UIAlertController.Create (title, message, UIAlertControllerStyle.ActionSheet);
                foreach (var action in actions) {
                    alertController.AddAction (UIAlertAction.Create (action.Title, action.UIStyle (), (UIAlertAction obj) => {
                        if (null != action.Action) {
                            action.Action ();
                        }
                    }));
                }
                var ppc = alertController.PopoverPresentationController;
                if (null != ppc) {
                    ppc.SourceView = parentView;
                    ppc.SourceRect = parentView.Bounds;
                    ppc.PermittedArrowDirections = UIPopoverArrowDirection.Any;
                }
                parentViewController.PresentViewController (alertController, true, null);

            } else {

                // iOS 7.  Use UIActionSheet.
                var actionSheet = new UIActionSheet ();
                for (int i = 0; i < actions.Length; ++i) {
                    actionSheet.AddButton (actions [i].Title);
                    switch (actions [i].Style) {
                    case NcAlertActionStyle.Cancel:
                        actionSheet.CancelButtonIndex = i;
                        break;
                    case NcAlertActionStyle.Destructive:
                        actionSheet.DestructiveButtonIndex = i;
                        break;
                    }
                }
                EventHandler<UIButtonEventArgs> clickAction = null;
                clickAction = (object sender, UIButtonEventArgs e) => {
                    NcAssert.True (0 <= e.ButtonIndex && e.ButtonIndex < actions.Length,
                        string.Format ("The index of the action sheet button that was clicked ({0}) is outside of the expected range (0..{1}).",
                            e.ButtonIndex, actions.Length - 1));
                    if (null != actions [e.ButtonIndex].Action) {
                        actions [e.ButtonIndex].Action ();
                    }
                    actionSheet.Clicked -= clickAction;
                };
                actionSheet.Clicked += clickAction;
                actionSheet.ShowInView (parentView);
            }
        }

        /// <summary>
        /// Show an action sheet with custom actions.
        /// </summary>
        public static void Show (
            UIView parentView, UIViewController parentViewController, params NcAlertAction[] actions)
        {
            Show (parentView, parentViewController, null, null, actions);
        }
    }
}
