//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Utils;
using CoreGraphics;
using NachoCore;

namespace NachoClient.iOS
{
    /// <summary>
    /// A wrapper for an action sheet, which is UIAlertController in action sheet mode.
    /// </summary>
    public class NcActionSheet
    {
        /// <summary>
        /// Show an action sheet with custom actions.
        /// </summary>
        private static void Show (
            UIView anchorView, UIBarButtonItem anchorButton, UIViewController parentViewController, string title, string message, params NcAlertAction[] actions)
        {
            var alertController = UIAlertController.Create (title, message, UIAlertControllerStyle.ActionSheet);
            foreach (var action in actions) {
                alertController.AddAction (UIAlertAction.Create (action.Title, action.UIStyle (), (UIAlertAction obj) => {
                    NcApplication.Instance.TelemetryService.RecordUiAlertView (title ?? message ?? "[No title]", action.Title);
                    if (null != action.Action) {
                        action.Action ();
                    }
                }));
            }
            var ppc = alertController.PopoverPresentationController;
            if (null != ppc) {
                if (null != anchorButton) {
                    ppc.BarButtonItem = anchorButton;
                } else {
                    ppc.SourceView = anchorView;
                    CGSize windowSize = parentViewController.View.Frame.Size;
                    CGSize viewSize = anchorView.Frame.Size;
                    if (viewSize.Width * viewSize.Height * 2 > windowSize.Width * windowSize.Height) {
                        // The view takes up more than half the screen.  Anchor the alert to the center of the view.
                        ppc.SourceRect = new CGRect (viewSize.Width / 2, viewSize.Height / 2, 0, 0);
                    } else {
                        // The view is small.  Anchor the alert to the outside of the view.
                        ppc.SourceRect = anchorView.Bounds;
                    }
                }
                ppc.PermittedArrowDirections = UIPopoverArrowDirection.Any;
            }
            parentViewController.PresentViewController (alertController, true, null);
        }

        public static void Show (
            UIView anchorView, UIViewController parentViewController, string title, string message, params NcAlertAction[] actions)
        {
            Show (anchorView, null, parentViewController, title, message, actions);
        }

        public static void Show (
            UIBarButtonItem anchorButton, UIViewController parentViewController, string title, string message, params NcAlertAction[] actions)
        {
            Show (null, anchorButton, parentViewController, title, message, actions);
        }

        /// <summary>
        /// Show an action sheet with custom actions.
        /// </summary>
        public static void Show (
            UIView anchorView, UIViewController parentViewController, params NcAlertAction[] actions)
        {
            Show (anchorView, parentViewController, null, null, actions);
        }
    }
}
