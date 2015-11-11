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

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            NachoCore.Utils.NcAbate.HighPriority ("NcUIViewController ViewDidLoad");
        }

        public override void ViewWillAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLAPPEAR + "_BEGIN");
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
            }
            base.ViewWillAppear (animated);
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLAPPEAR + "_END");
        }

        public override void ViewDidAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR + "_BEGIN");
            base.ViewDidAppear (animated);
            NachoCore.Utils.NcAbate.RegularPriority ("NcUIViewController ViewDidAppear");
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR + "_END");
        }

        public override void ViewWillDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR + "_BEGIN");
            base.ViewWillDisappear (animated);
            if (null != ViewDisappearing) {
                ViewDisappearing (this, EventArgs.Empty);
            }
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillHideNotification);
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillShowNotification);
            }
            if (ShouldEndEditing) {
                View.EndEditing (true);
            }
            NachoCore.Utils.NcAbate.RegularPriority ("NcUIViewController ViewWillDisappear");
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR + "_END");
        }

        public override void ViewDidDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR + "_BEGIN");
            base.ViewDidDisappear (animated);
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR + "_END");
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
            NcTimeStamp.Add ("ViewDidLoad:START");
            // Not cool for all subclasses yet (eg GeneralSettingsViewController).
            CreateViewHierarchy ();
            base.ViewDidLoad ();
            NcTimeStamp.Add ("ConfigureAndLayout:START");
            ConfigureAndLayout ();
            NcTimeStamp.Add ("ViewDidLoad:DONE");
        }

        public override void ViewWillAppear (bool animated)
        {
            NcTimeStamp.Add ("ViewWillAppear:START");
            base.ViewWillAppear (animated);
            // Force the view hierarchy to be created by accessing the View property.
            this.View.GetHashCode ();
            NcTimeStamp.Add ("ViewWillAppear:DONE");
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            if (this.IsViewLoaded && null == this.NavigationController) {
                Cleanup ();
                ViewHelper.DisposeViewHierarchy (View);
                View = null;
            }
        }
    }
}

