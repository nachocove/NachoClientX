//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;

namespace NachoClient.iOS
{
    public class NcKeyboardSpy
    {
        public bool keyboardShowing;
        public nfloat keyboardHeight;

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
            NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
            NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
        }

        public void Cleanup ()
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillHideNotification);
            NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillShowNotification);
        }

        private void OnKeyboardNotification (NSNotification notification)
        {
            //Check if the keyboard is becoming visible
            bool keyboardVisible = notification.Name == UIKeyboard.WillShowNotification;

            var orientation = UIApplication.SharedApplication.StatusBarOrientation;
            bool landscape = (orientation == UIInterfaceOrientation.LandscapeLeft) || (orientation == UIInterfaceOrientation.LandscapeRight);
            if (keyboardVisible) {
                var keyboardFrame = UIKeyboard.FrameEndFromNotification (notification);
                keyboardHeight = (landscape ? keyboardFrame.Width : keyboardFrame.Height);
            } else {
                keyboardHeight = 0;
            }
        }
    }
}

