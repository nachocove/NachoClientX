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
        //public UITextView statusMessage;
        protected float yOffset;
        public bool foundServer = false;

        int numDots = 1;
        NSTimer loadingTimer;
        public UILabel statusMessage;

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
            this.BackgroundColor = A.Color_NachoGreen;
            this.Frame = new RectangleF (0, 0, owner.View.Frame.Width, owner.View.Frame.Height);

            yOffset = 25f;
            
            statusMessage = new UILabel (new RectangleF(25, yOffset, Frame.Width- 50, 100));
            statusMessage.Text = "Nacho Mail Is Locating Your Server...";
            statusMessage.Font = A.Font_AvenirNextRegular24;
            statusMessage.Lines = 3;
            statusMessage.TextColor = UIColor.White;
            statusMessage.TextAlignment = UITextAlignment.Center;
            this.Add (statusMessage);

            yOffset = statusMessage.Frame.Bottom + 250;

            UIButton cancelValidation = new UIButton (new System.Drawing.RectangleF (0, yOffset, Frame.Width, 40));
            cancelValidation.Tag = 3;
            cancelValidation.Layer.CornerRadius = 10.0f;
            cancelValidation.TitleLabel.Font = A.Font_AvenirNextRegular28;
            cancelValidation.BackgroundColor = A.Color_NachoGreen;
            cancelValidation.TitleLabel.TextAlignment = UITextAlignment.Center;
            cancelValidation.SetTitle ("Cancel", UIControlState.Normal);
            cancelValidation.SetTitleColor (A.Color_SystemBlue, UIControlState.Normal);
            cancelValidation.TouchUpInside += (object sender, EventArgs e) => {
                owner.stopBeIfRunning ();
                owner.ConfigureView (AdvancedLoginViewController.LoginStatus.EnterInfo);
                DismissView ();
            };
            this.Add (cancelValidation);



            this.Hidden = true;
        }

        public void ShowView ()
        {
            this.Hidden = false;
            owner.NavigationItem.Title = "";
            owner.NavigationController.NavigationBar.SetBackgroundImage (new UIImage(), UIBarMetrics.Default);
            owner.NavigationController.NavigationBar.BackgroundColor = A.Color_NachoGreen;
            owner.NavigationController.NavigationBar.ShadowImage = new UIImage ();
            startLoadingAnimation ();
        }

        public void DismissView ()
        {
            loadingTimer.Invalidate();
            loadingTimer = null;
            this.Hidden = true;
            owner.NavigationItem.Title = "Account Setup";
        }

        public void startLoadingAnimation ()
        {
            statusMessage.Text = "Nacho Mail Is Locating Your Server";
            //loadingTimer = new NSTimer(NSDate.Now, System.TimeSpan.FromSeconds(.5), () => timerAction(), true);
            loadingTimer = NSTimer.CreateRepeatingScheduledTimer (TimeSpan.FromSeconds (.5), delegate {
                timerAction ();
            });

        }

        public void timerAction()
        {
            if (!foundServer) {
                if (numDots == 1) {
                    statusMessage.Text = "Nacho Mail Is Locating Your Server..";
                    numDots = 2;
                } else if (numDots == 2) {
                    statusMessage.Text = "Nacho Mail Is Locating Your Server...";
                    numDots = 3;
                } else {
                    statusMessage.Text = "Nacho Mail Is Locating Your Server.";
                    numDots = 1;
                }
            } else {
                if (numDots == 1) {
                    statusMessage.Text = "Found Your Server..";
                    numDots = 2;
                } else if (numDots == 2) {
                    statusMessage.Text = "Found Your Server...";
                    numDots = 3;
                } else {
                    statusMessage.Text = "Found Your Server.";
                    numDots = 1;
                }
            }


        }
    }
}
