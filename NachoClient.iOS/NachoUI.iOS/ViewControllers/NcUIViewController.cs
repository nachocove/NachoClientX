//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class NcUIViewController : UIViewController
    {
        private string ClassName;

        public event EventHandler ViewDisappearing;

        protected nfloat keyboardHeight;

        NSObject KeyboardWillShowNotificationToken;
        NSObject KeyboardWillHideNotificationToken;

        public NcUIViewController () : base ()
        {
            Initialize ();
        }

        public NcUIViewController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        public NcUIViewController (string nibName, NSBundle bundle) : base (nibName, bundle)
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
            if (HandlesKeyboardNotifications) {
                KeyboardWillHideNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
                KeyboardWillShowNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
            }
            base.ViewWillAppear (animated);
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
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.RemoveObserver (KeyboardWillHideNotificationToken);
                NSNotificationCenter.DefaultCenter.RemoveObserver (KeyboardWillShowNotificationToken);
            }
            if (ShouldEndEditing) {
                View.EndEditing (true);
            }
        }

        public override void ViewDidDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR);
            base.ViewDidDisappear (animated);
        }

        public virtual bool ShouldEndEditing {
            get { return true; }
        }

        public virtual bool HandlesKeyboardNotifications {
            get { return true; }
        }

        protected virtual void OnKeyboardChanged ()
        {
        }

        protected virtual bool ShouldCleanupDuringDidDisappear
        {
            get {
                return IsViewLoaded && (IsBeingDismissed || IsMovingFromParentViewController);
            }
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
    }

    public abstract class NcUIViewControllerNoLeaks : NcUIViewController
    {
        public NcUIViewControllerNoLeaks ()
            : base ()
        {
        }

        public NcUIViewControllerNoLeaks (IntPtr handle)
            : base (handle)
        {
        }

        public NcUIViewControllerNoLeaks (string nibName, NSBundle bundle)
            : base (nibName, bundle)
        {
        }

        protected abstract void CreateViewHierarchy ();

        protected abstract void ConfigureAndLayout ();

        protected abstract void Cleanup ();

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            CreateViewHierarchy ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            // Force the view hierarchy to be created by accessing the View property.
            this.View.GetHashCode ();
            ConfigureAndLayout ();
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            if (ShouldCleanupDuringDidDisappear) {
                Cleanup ();
                ViewHelper.DisposeViewHierarchy (View);
                View = null;
            }
        }
    }
}

