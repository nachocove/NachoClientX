// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using CoreGraphics;
using Foundation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using UIKit;

namespace NachoClient.iOS
{
    public partial class SupportViewController : NcUIViewControllerNoLeaks
    {
        protected static readonly nfloat INDENT = 18;

        SwitchAccountButton switchAccountButton;

        protected UITapGestureRecognizer messageTapGesture;
        protected UITapGestureRecognizer.Token messageTapGestureHandlerToken;
        protected const int MESSAGE_TAP_VIEW_TAG = 111;

        protected UITapGestureRecognizer callTapGesture;
        protected UITapGestureRecognizer.Token callTapGestureHandlerToken;
        protected const int CALL_TAP_VIEW_TAG = 222;

        public SupportViewController (IntPtr handle) : base (handle)
        {
        }

        protected override void CreateViewHierarchy ()
        {
            View.BackgroundColor = A.Color_NachoBackgroundGray;
            Util.ConfigureNavBar (false, this.NavigationController);

            switchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
            NavigationItem.TitleView = switchAccountButton;

            UIView supportView = new UIView (new CGRect (A.Card_Horizontal_Indent, A.Card_Vertical_Indent, View.Frame.Width - A.Card_Horizontal_Indent * 2, View.Frame.Height - 24 - 120));
            supportView.BackgroundColor = UIColor.White;
            supportView.Layer.CornerRadius = A.Card_Corner_Radius;
            supportView.Layer.BorderColor = A.Card_Border_Color;
            supportView.Layer.BorderWidth = A.Card_Border_Width;

            nfloat yOffset = INDENT;

            UIImageView nachoLogoImageView;
            using (var nachoLogo = UIImage.FromBundle ("Bootscreen-1")) {
                nachoLogoImageView = new UIImageView (nachoLogo);
            }
            nachoLogoImageView.Frame = new CGRect (supportView.Frame.Width / 2 - 40, yOffset, 80, 80);
            supportView.Add (nachoLogoImageView);

            yOffset = nachoLogoImageView.Frame.Bottom + 26;

            UILabel happyToHearLabel = new UILabel (new CGRect (INDENT, yOffset, supportView.Frame.Width - INDENT * 2, 50));
            happyToHearLabel.Font = A.Font_AvenirNextDemiBold17;
            happyToHearLabel.TextColor = A.Color_NachoGreen;
            happyToHearLabel.TextAlignment = UITextAlignment.Center;
            happyToHearLabel.Lines = 2;
            happyToHearLabel.LineBreakMode = UILineBreakMode.WordWrap;
            happyToHearLabel.Text = "We're always more than happy to hear from you.";
            supportView.AddSubview (happyToHearLabel);

            yOffset = happyToHearLabel.Frame.Bottom + 14;

            Util.AddHorizontalLine (0, yOffset, supportView.Frame.Width, A.Color_NachoBorderGray, supportView);

            yOffset += INDENT;
            nfloat topEmailCellYVal = yOffset;

            UIImageView emailIconImage;
            using (var emailIcon = UIImage.FromBundle ("contacts-email")) {
                emailIconImage = new UIImageView (emailIcon);
            }
            emailIconImage.Frame = new CGRect (INDENT, yOffset, emailIconImage.Frame.Width, emailIconImage.Frame.Height);
            supportView.AddSubview (emailIconImage);

            UILabel sendUsEmailLabel = new UILabel (new CGRect (emailIconImage.Frame.Right + 12, yOffset, View.Frame.Width - 100, 40));
            sendUsEmailLabel.Font = A.Font_AvenirNextMedium14;
            sendUsEmailLabel.TextColor = A.Color_NachoBlack;
            sendUsEmailLabel.TextAlignment = UITextAlignment.Left;
            sendUsEmailLabel.Lines = 2;
            sendUsEmailLabel.LineBreakMode = UILineBreakMode.WordWrap;
            sendUsEmailLabel.Text = "Send us a message at support@nachocove.com";
            sendUsEmailLabel.SizeToFit ();
            supportView.AddSubview (sendUsEmailLabel);

            new ViewFramer (emailIconImage).Y (sendUsEmailLabel.Center.Y - emailIconImage.Frame.Height / 2);
            yOffset = sendUsEmailLabel.Frame.Bottom + INDENT;
            nfloat bottomEmailCellYVal = yOffset;

            UIView messageTapView = new UIView (new CGRect (0, topEmailCellYVal, supportView.Frame.Width, bottomEmailCellYVal - topEmailCellYVal));
            messageTapView.BackgroundColor = UIColor.Clear;
            messageTapView.Tag = MESSAGE_TAP_VIEW_TAG;
            messageTapView.UserInteractionEnabled = true;

            messageTapGesture = new UITapGestureRecognizer ();
            messageTapGesture.NumberOfTapsRequired = 1;
            messageTapGestureHandlerToken = messageTapGesture.AddTarget (MessageSingleTapHandler);
            messageTapView.AddGestureRecognizer (messageTapGesture);

            supportView.AddSubview (messageTapView);

            Util.AddHorizontalLine (0, yOffset, supportView.Frame.Width, A.Color_NachoBorderGray, supportView);

            yOffset += INDENT;

            UIImageView callIconImage;
            using (var callIcon = UIImage.FromBundle ("contacts-call")) {
                callIconImage = new UIImageView (callIcon);
            }
            callIconImage.Frame = new CGRect (INDENT, yOffset, callIconImage.Frame.Width, callIconImage.Frame.Height);
            supportView.AddSubview (callIconImage);

            UILabel callUsLabel = new UILabel (new CGRect (callIconImage.Frame.Right + 12, yOffset, 220, 30));
            callUsLabel.Font = A.Font_AvenirNextMedium14;
            callUsLabel.TextColor = A.Color_NachoBlack;
            callUsLabel.TextAlignment = UITextAlignment.Left;
            callUsLabel.Text = "Call us at +1 (971) 803-6226";
            supportView.AddSubview (callUsLabel);

            yOffset = callUsLabel.Frame.Bottom + INDENT;
            nfloat bottomCallCellYVal = yOffset;

            UIView callTapView = new UIView (new CGRect (0, bottomEmailCellYVal, supportView.Frame.Width, bottomCallCellYVal - bottomEmailCellYVal));
            callTapView.BackgroundColor = UIColor.Clear;
            callTapView.UserInteractionEnabled = true; 
            callTapView.Tag = CALL_TAP_VIEW_TAG;

            callTapGesture = new UITapGestureRecognizer ();
            callTapGesture.NumberOfTapsRequired = 1;
            callTapGestureHandlerToken = callTapGesture.AddTarget (CallSingleTapHandler);
            callTapView.AddGestureRecognizer (callTapGesture);

            supportView.AddSubview (callTapView);

            Util.AddHorizontalLine (0, yOffset, supportView.Frame.Width, A.Color_NachoBorderGray, supportView);

            UILabel versionLabel = new UILabel (new CGRect (supportView.Frame.Width / 2 - 75, supportView.Frame.Bottom - 50, 150, 20));
            versionLabel.Font = A.Font_AvenirNextRegular10;
            versionLabel.TextColor = A.Color_NachoBlack;
            versionLabel.TextAlignment = UITextAlignment.Center;
            versionLabel.Text = "Nacho Mail version " + Util.GetVersionNumber ();//"Nacho Mail version 0.9";
            supportView.AddSubview (versionLabel);

            View.AddSubview (supportView);
        }

        protected override void ConfigureAndLayout ()
        {
            switchAccountButton.SetAccountImage (NcApplication.Instance.Account);
        }

        private void MessageSingleTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                PerformSegue ("SupportToSupportMessage", this);
            }
        }

        private void CallSingleTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                UIApplication.SharedApplication.OpenUrl (new NSUrl ("telprompt://19718036226"));
            }
        }

        void SwitchAccountButtonPressed ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        void SwitchToAccount (McAccount account)
        {
            switchAccountButton.SetAccountImage (account);
        }

        protected override void Cleanup ()
        {
            messageTapGesture.RemoveTarget (messageTapGestureHandlerToken);
            messageTapGesture.ShouldRecognizeSimultaneously = null;
            UIView messageTapView = (UIView)View.ViewWithTag (MESSAGE_TAP_VIEW_TAG);
            messageTapView.RemoveGestureRecognizer (messageTapGesture);

            callTapGesture.RemoveTarget (callTapGestureHandlerToken);
            callTapGesture.ShouldRecognizeSimultaneously = null;
            UIView callTapView = (UIView)View.ViewWithTag (CALL_TAP_VIEW_TAG);
            callTapView.RemoveGestureRecognizer (callTapGesture);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
        }
    }
}
