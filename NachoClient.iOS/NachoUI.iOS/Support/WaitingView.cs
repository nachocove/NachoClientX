//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using UIImageEffectsBinding;
using MonoTouch.CoreGraphics;

namespace NachoClient.iOS
{
    [Register ("WaitingView")]

    public class WaitingView: UIView
    {
        AdvancedLoginViewController owner;
        public UITextView statusMessage;

        public WaitingView ()
        {

        }

        public void SetOwner (AdvancedLoginViewController owner)
        {
            this.owner = owner;
        }

        public WaitingView (RectangleF frame)
        {
            this.Frame = frame;
        }

        public WaitingView (IntPtr handle) : base (handle)
        {

        }

        public void CreateView ()
        {
            this.BackgroundColor = UIColor.Gray.ColorWithAlpha (.4f);
            this.Frame = new RectangleF (0, 0, owner.View.Frame.Width, owner.View.Frame.Height);
            UIView waitingView = new UIView (new System.Drawing.RectangleF (60, 100, this.Frame.Width - 120, 146));
            waitingView.Layer.CornerRadius = 7.0f;
            waitingView.BackgroundColor = UIColor.White;
            waitingView.Alpha = 1.0f;
            this.Add (waitingView);

            statusMessage = new UITextView (new System.Drawing.RectangleF (8, 2, waitingView.Frame.Width - 16, waitingView.Frame.Height / 2.4f));
            statusMessage.Layer.BorderWidth = 0.0f;
            statusMessage.BackgroundColor = UIColor.White;
            statusMessage.Alpha = 1.0f;
            statusMessage.Font = UIFont.SystemFontOfSize (17);
            statusMessage.TextColor = UIColor.Black;
            statusMessage.Text = "Locating Your Server...";
            statusMessage.TextAlignment = UITextAlignment.Center;
            statusMessage.Editable = false;
            waitingView.Add (statusMessage);

            UIActivityIndicatorView theSpinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.WhiteLarge);
            theSpinner.Alpha = 1.0f;
            theSpinner.HidesWhenStopped = true;
            theSpinner.Tag = 1;
            theSpinner.Frame = new System.Drawing.RectangleF (waitingView.Frame.Width / 2 - 20, 50, 40, 40);
            theSpinner.Color = A.Color_SystemBlue;
            theSpinner.StartAnimating ();
            waitingView.Add (theSpinner);

            UIView cancelLine = new UIView (new System.Drawing.RectangleF (0, 105, waitingView.Frame.Width, .5f));
            cancelLine.BackgroundColor = UIColor.LightGray;
            cancelLine.Tag = 2;
            waitingView.Add (cancelLine);

            UIButton cancelValidation = new UIButton (new System.Drawing.RectangleF (0, 106, waitingView.Frame.Width, 40));
            cancelValidation.Tag = 3;
            cancelValidation.Layer.CornerRadius = 10.0f;
            cancelValidation.BackgroundColor = UIColor.White;
            cancelValidation.TitleLabel.TextAlignment = UITextAlignment.Center;
            cancelValidation.SetTitle ("Cancel", UIControlState.Normal);
            cancelValidation.SetTitleColor (A.Color_SystemBlue, UIControlState.Normal);
            cancelValidation.TouchUpInside += (object sender, EventArgs e) => {
                owner.stopBeIfRunning ();
                owner.ConfigureView (AdvancedLoginViewController.LoginStatus.EnterInfo);
                DismissView ();
            };
            waitingView.Add (cancelValidation);
            this.Hidden = true;
        }

        public void ShowView ()
        {
            this.Hidden = false;
        }

        public void DismissView ()
        {
            this.Hidden = true;
        }
    }
}
