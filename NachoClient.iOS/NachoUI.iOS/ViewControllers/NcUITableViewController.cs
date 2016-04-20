//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Utils;
using Foundation;

namespace NachoClient.iOS
{
    public class NcUITableViewController : UITableViewController
    {

        NSObject KeyboardWillShowNotificationToken;
        NSObject KeyboardWillHideNotificationToken;

        private string ClassName;
        public event EventHandler ViewDisappearing;

        protected nfloat keyboardHeight;

        public NcUITableViewController () : base ()
        {
            Initialize ();
        }

        public NcUITableViewController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        public NcUITableViewController (UITableViewStyle style) : base (style)
        {
            Initialize ();
        }

        private void Initialize ()
        {
            ClassName = this.GetType ().Name;
        }

        public override void ViewWillAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLAPPEAR);
            base.ViewWillAppear (animated);
            KeyboardWillHideNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
            KeyboardWillShowNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
        }

        public override void ViewDidAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR);
            base.ViewDidAppear (animated);
        }

        public override void ViewWillDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR);
            base.ViewWillDisappear (animated);
            if (null != ViewDisappearing) {
                ViewDisappearing (this, EventArgs.Empty);
            }
        }

        public override void ViewDidDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR);
            base.ViewDidDisappear (animated);
            NSNotificationCenter.DefaultCenter.RemoveObserver (KeyboardWillHideNotificationToken);
            NSNotificationCenter.DefaultCenter.RemoveObserver (KeyboardWillShowNotificationToken);
        }

        private void OnKeyboardNotification (NSNotification notification)
        {
            if (IsViewLoaded && View.Window != null) {
                //Check if the keyboard is becoming visible
                bool visible = notification.Name == UIKeyboard.WillShowNotification;
                //Start an animation, using values from the keyboard
                UIView.BeginAnimations ("AnimateForKeyboard");
                UIView.SetAnimationBeginsFromCurrentState (true);
                UIView.SetAnimationDuration (UIKeyboard.AnimationDurationFromNotification (notification));
                UIView.SetAnimationCurve ((UIViewAnimationCurve)UIKeyboard.AnimationCurveFromNotification (notification));
                //Pass the notification, calculating keyboard height, etc.
                var oldHeight = keyboardHeight;
                if (visible) {
                    var keyboardFrameInScreen = UIKeyboard.FrameEndFromNotification (notification);
                    var keyboardFrameInWindow = View.Window.ConvertRectFromWindow (keyboardFrameInScreen, null);
                    var keyboardFrameInView = View.ConvertRectFromView (keyboardFrameInWindow, View.Window);
                    keyboardHeight = View.Frame.Height - keyboardFrameInView.Top;
                } else {
                    keyboardHeight = 0;
                }
                if (oldHeight != keyboardHeight) {
                    OnKeyboardChanged ();
                }
                //Commit the animation
                UIView.CommitAnimations (); 
            }
        }

        protected virtual void OnKeyboardChanged ()
        {
        }
    }
}

