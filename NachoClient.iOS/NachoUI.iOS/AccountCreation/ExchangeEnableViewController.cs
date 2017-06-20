//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using UIKit;
using CoreGraphics;
using MessageUI;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{

    public interface ExchangeEnableViewDelegate
    {
        void ExchangeEnableViewDidComplete (ExchangeEnableViewController vc, bool exchangeEnabled);
    }

    public class ExchangeEnableViewController : NcUIViewController, ThemeAdopter
    {

        ExchangeEnableView _ExchangeEnableView;

        public ExchangeEnableViewDelegate Delegate;
        public McAccount.AccountServiceEnum Service { get; private set; }

        public ExchangeEnableViewController (McAccount.AccountServiceEnum service)
        {
            Service = service;
        }

        public override UIStatusBarStyle PreferredStatusBarStyle ()
        {
            return UIStatusBarStyle.LightContent;
		}

        #region View Lifecycle

        public override void LoadView ()
        {
            View = new UIView (new CGRect (0, 0, 100, 100));
            _ExchangeEnableView = new ExchangeEnableView ();
            _ExchangeEnableView.Frame = View.Bounds;
            _ExchangeEnableView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            View.AddSubview (_ExchangeEnableView);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            _ExchangeEnableView.CloseButton.TouchUpInside += Close;
            _ExchangeEnableView.EnterCodeButton.TouchUpInside += ShowCodeEntry;
            _ExchangeEnableView.SubmitButton.TouchUpInside += SubmitCode;
            _ExchangeEnableView.RequestCodeButton.TouchUpInside += RequestCode;
            _ExchangeEnableView.AccountAvatarView.Image = UIImage.FromBundle (Util.GetAccountServiceImageName (Service));
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            AdoptTheme (Theme.Active);
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            if (ShouldCleanupDuringDidDisappear){
                Cleanup ();
            }
        }

        void Cleanup()
        {
            _ExchangeEnableView.CloseButton.TouchUpInside -= Close;
            _ExchangeEnableView.EnterCodeButton.TouchUpInside -= ShowCodeEntry;
            _ExchangeEnableView.SubmitButton.TouchUpInside -= SubmitCode;
            _ExchangeEnableView.RequestCodeButton.TouchUpInside -= RequestCode;
        }

        protected override void OnKeyboardChanged ()
        {
            _ExchangeEnableView.Frame = new CGRect (0, 0, View.Bounds.Width, View.Bounds.Height - keyboardHeight);
        }

        #endregion

        #region Theme

        Theme AdoptedTheme;

        public void AdoptTheme(Theme theme)
        {
            if (theme != AdoptedTheme){
                AdoptedTheme = theme;
                _ExchangeEnableView.AdoptTheme (theme);
            }
        }

        #endregion

        #region User Actions

        void ShowCodeEntry (object sender, EventArgs e)
        {
            ShowCodeEntry ();
        }

        void SubmitCode (object sender, EventArgs e)
        {
            CheckCode (_ExchangeEnableView.CodeField.Text);
		}

		void RequestCode (object sender, EventArgs e)
		{
            RequestCode ();
		}

        void Close (object sender, EventArgs e)
        {
            Close ();
        }

        #endregion

        #region Private Helpers

        void ShowCodeEntry()
        {
            _ExchangeEnableView.IsShowingEntry = true;
            _ExchangeEnableView.CodeField.BecomeFirstResponder ();
        }

        void RequestCode()
        {
            var to = "info@nachocove.com";
            var subject = "Using Exchange with Nacho Mail";
            var text = "Hello,\n\nI'm interested in using Nacho Mail with an Exchange account.  Please send me a code to enable Exchange accounts.\n\nThanks";
            var account = NcApplication.Instance.DefaultEmailAccount;
            if (account == null){
                if (MFMailComposeViewController.CanSendMail) {
                    var controller = new MFMailComposeViewController ();
                    controller.SetToRecipients (new string[] { to });
                    controller.SetSubject (subject);
                    controller.SetMessageBody (text, isHtml: false);
                    controller.Finished += (sender, e) => {
                        e.Controller.DismissViewController (animated: true, completionHandler: null);
                    };
                    PresentViewController (controller, animated: true, completionHandler: null);
                } else {
                    var alert = UIAlertController.Create ("Send Request", "Please send a request to info@nachocove.com", UIAlertControllerStyle.Alert);
                    alert.AddAction (UIAlertAction.Create ("OK", UIAlertActionStyle.Default,(obj) => {}));
                    PresentViewController (alert, animated: true, completionHandler: null);
                }
            }else{
                var message = new McEmailMessage ();
                message.AccountId = account.Id;
                message.To = to;
                message.Subject = subject;
                var controller = new MessageComposeViewController (account);
                controller.Composer.Message = message;
                controller.Composer.InitialText = text;
                controller.Present ();
            }
        }

        void CheckCode (string code)
        {
            bool verified = NachoCore.Utils.PermissionManager.Instance.VerifyExchangeCode (code);
            if (!verified){
                var alert = UIAlertController.Create ("Incorrect Code", "The code you provided is not valid.  Please verify what was entered and try again.", UIAlertControllerStyle.Alert);
                alert.AddAction (UIAlertAction.Create ("OK", UIAlertActionStyle.Default,(obj) => {}));
                PresentViewController (alert, animated: true, completionHandler: null);
            }else{
                Delegate?.ExchangeEnableViewDidComplete (this, true);
            }
        }

        void Close()
        {
            Delegate?.ExchangeEnableViewDidComplete (this, false);
        }

        #endregion

        class ExchangeEnableView : UIView, ThemeAdopter
		{
            public UIView StatusBarBackground { get; private set;  }
			public UIButton CloseButton { get; private set; }
			public UIScrollView ScrollView { get; private set; }
            UIView ContainerView;
			public UIImageView AccountAvatarView { get; private set; }
			public UILabel MessageLabel { get; private set; }
			public UIButton RequestCodeButton { get; private set; }
			public UIButton EnterCodeButton { get; private set; }
			public UITextField CodeField { get; private set; }
			public UIButton SubmitButton { get; private set; }

            bool _IsShowingEntry;
            public bool IsShowingEntry {
                get{
                    return _IsShowingEntry;
                }
                set {
                    _IsShowingEntry = value;
                    if (_IsShowingEntry){
                        MessageLabel.Text = "Enter the access code to enable Exchange accounts";
                    }else{
                        MessageLabel.Text = "Please contact Nacho Cove to inquire about support for enterprise Exchange/ActiveSync accounts";
                    }
                    SetNeedsLayout ();
                }
            }

            public ExchangeEnableView () : base(new CGRect(0, 0, 100, 100))
			{
                StatusBarBackground = new UIView ();
                AddSubview (StatusBarBackground);

				ScrollView = new UIScrollView (Bounds);
				AddSubview (ScrollView);

				CloseButton = new UIButton (UIButtonType.Custom);
                CloseButton.SetImage (UIImage.FromBundle ("gen-close"), UIControlState.Normal);
				AddSubview (CloseButton);

                ContainerView = new UIView ();
                ScrollView.AddSubview (ContainerView);

                AccountAvatarView = new UIImageView (new CGRect(0.0f, 0.0f, 80.0f, 80.0f));
                AccountAvatarView.Layer.CornerRadius = AccountAvatarView.Frame.Width / 2.0f;
                AccountAvatarView.ClipsToBounds = true;
                ContainerView.AddSubview (AccountAvatarView);

                MessageLabel = new UILabel ();
                MessageLabel.Lines = 0;
                MessageLabel.LineBreakMode = UILineBreakMode.WordWrap;
                MessageLabel.TextAlignment = UITextAlignment.Center;
                ContainerView.AddSubview (MessageLabel);

                RequestCodeButton = new NcSimpleColorButton ();
                RequestCodeButton.Layer.CornerRadius = 6.0f;
                RequestCodeButton.SetTitle ("Contact Nacho Cove", UIControlState.Normal);
                ContainerView.AddSubview (RequestCodeButton);

                EnterCodeButton = new UIButton (UIButtonType.Custom);
                EnterCodeButton.SetTitle ("Have a code?", UIControlState.Normal);
                ContainerView.AddSubview (EnterCodeButton);

                CodeField = new UITextField ();
                CodeField.Placeholder = "Code";
                CodeField.BackgroundColor = UIColor.White;
                CodeField.BorderStyle = UITextBorderStyle.RoundedRect;
				ContainerView.AddSubview (CodeField);

                SubmitButton = new NcSimpleColorButton ();
                SubmitButton.Layer.CornerRadius = 6.0f;
				SubmitButton.SetTitle ("Enable Exchange", UIControlState.Normal);
				ContainerView.AddSubview (SubmitButton);

                IsShowingEntry = false;
            }

            public override void LayoutSubviews ()
            {
                var statusFrame = ConvertRectFromView (UIApplication.SharedApplication.StatusBarFrame, Window);
                if (statusFrame.Width == 0){
                    statusFrame.Width = Bounds.Width;
                }
                StatusBarBackground.Frame = statusFrame;
                CloseButton.Frame = new CGRect (17.0f, 30.0f, 24.0f, 24.0f);
                ScrollView.Frame = new CGRect (0, statusFrame.Height, Bounds.Width, Bounds.Height - statusFrame.Height);

                ContainerView.Frame = ScrollView.Bounds;

                AccountAvatarView.Center = new CGPoint (ContainerView.Bounds.Width / 2.0f, AccountAvatarView.Frame.Size.Height / 2.0f);

                var textPadding = 40.0f;
                var textWidth = ContainerView.Bounds.Size.Width - textPadding - textPadding;
                var size = MessageLabel.SizeThatFits (new CGSize (textWidth, 99999999.0f));
                MessageLabel.Frame = new CGRect (textPadding, AccountAvatarView.Frame.Y + AccountAvatarView.Frame.Height + 30.0f, textWidth, size.Height);

                UIView lastView;
                if (_IsShowingEntry){
                    RequestCodeButton.Hidden = true;
                    EnterCodeButton.Hidden = true;
                    CodeField.Hidden = false;
                    SubmitButton.Hidden = false;
                    CodeField.Frame = new CGRect (textPadding, MessageLabel.Frame.Y + MessageLabel.Frame.Height + 30.0f, textWidth, 46.0f);
                    SubmitButton.Frame = new CGRect (textPadding, CodeField.Frame.Y + CodeField.Frame.Height + 10.0f, textWidth, 46.0f);
                    lastView = SubmitButton;
                }else{
                    RequestCodeButton.Hidden = false;
                    EnterCodeButton.Hidden = false;
                    CodeField.Hidden = true;
					SubmitButton.Hidden = true;
                    RequestCodeButton.Frame = new CGRect (textPadding, MessageLabel.Frame.Y + MessageLabel.Frame.Height + 30.0f, textWidth, 46.0f);
                    EnterCodeButton.Frame = new CGRect (textPadding, RequestCodeButton.Frame.Y + RequestCodeButton.Frame.Height + 10.0f, textWidth, EnterCodeButton.IntrinsicContentSize.Height);
                    lastView = EnterCodeButton;
                }

                var height = lastView.Frame.Y + lastView.Frame.Height;
                var padding = (ScrollView.Bounds.Height - height) / 2.0f;
                if (padding < 40.0f){
                    padding = 40.0f;
                }
                ContainerView.Frame = new CGRect (0.0f, padding, ScrollView.Bounds.Width, height);
                ScrollView.ContentSize = new CGSize (ScrollView.Bounds.Width, ContainerView.Frame.Height + padding + padding);
            }

            public void AdoptTheme (Theme theme)
            {
                BackgroundColor = theme.AccountCreationBackgroundColor;
                StatusBarBackground.BackgroundColor = theme.AccountCreationBackgroundColor;
                MessageLabel.TextColor = theme.AccountCreationTextColor;
                MessageLabel.Font = theme.DefaultFont.WithSize (17.0f);
                RequestCodeButton.BackgroundColor = theme.AccountCreationButtonColor;
                RequestCodeButton.SetTitleColor (theme.AccountCreationButtonTitleColor, UIControlState.Normal);
                SubmitButton.BackgroundColor = theme.AccountCreationButtonColor;
                SubmitButton.SetTitleColor (theme.AccountCreationButtonTitleColor, UIControlState.Normal);
                EnterCodeButton.SetTitleColor (theme.AccountCreationButtonColor, UIControlState.Normal);
                CodeField.Font = theme.DefaultFont.WithSize (17.0f);
                SetNeedsLayout ();
            }
        }
    }
}
