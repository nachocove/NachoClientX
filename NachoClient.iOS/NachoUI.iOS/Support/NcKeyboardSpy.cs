//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class NcKeyboardSpy
    {

        NSObject KeyboardWillShowNotificationToken;
        NSObject KeyboardWillHideNotificationToken;

        public bool keyboardShowing;
        public nfloat keyboardHeight;
        CGRect keyboardFrame;

        /// <summary>
        /// All view controllers, except for UITableViewControllers, are responsible
        /// for adjusting their views to reflect the presence (or the absence) of the
        /// keyboard. The system sends a notification when the status changes however
        /// there's no way to query the system to see if the keyboard is showing. The
        /// problem is that a newly created view cannot tell if the keyboard is up or
        /// not during initialization.
        /// </summary>
        public NcKeyboardSpy ()
        {
        }

        private static NcKeyboardSpy instance;
        private static object syncRoot = new Object ();

        public static NcKeyboardSpy Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new NcKeyboardSpy ();
                    }
                }
                return instance; 
            }
        }

        public void Init ()
        {
            KeyboardWillHideNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
            KeyboardWillShowNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
        }

        public void Cleanup ()
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver (KeyboardWillHideNotificationToken);
            NSNotificationCenter.DefaultCenter.RemoveObserver (KeyboardWillShowNotificationToken);
        }

        public nfloat KeyboardHeightInView (UIView view)
        {
            if (view.Window != null) {
                var keyboardFrameInWindow = view.Window.ConvertRectFromWindow (keyboardFrame, null);
                var keyboardFrameInView = view.ConvertRectFromView (keyboardFrameInWindow, view.Window);
                return (nfloat)Math.Max (0.0f, view.Frame.Height - keyboardFrameInView.Top);
            }
            return 0.0f;
        }

        private void OnKeyboardNotification (NSNotification notification)
        {
            //Check if the keyboard is becoming visible
            bool keyboardVisible = notification.Name == UIKeyboard.WillShowNotification;

            var orientation = UIApplication.SharedApplication.StatusBarOrientation;
            bool landscape = (orientation == UIInterfaceOrientation.LandscapeLeft) || (orientation == UIInterfaceOrientation.LandscapeRight);
            keyboardFrame = UIKeyboard.FrameEndFromNotification (notification);
            if (keyboardVisible) {
                keyboardShowing = true;
                keyboardHeight = (landscape ? keyboardFrame.Width : keyboardFrame.Height);
            } else {
                keyboardShowing = false;
                keyboardHeight = 0;
            }
        }
    }
}

