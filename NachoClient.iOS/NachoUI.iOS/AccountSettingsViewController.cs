// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;
using CoreGraphics;
using NachoCore.Model;
using NachoCore.Utils;
using System.Linq;
using NachoCore;
using NachoPlatform;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using Xamarin.Auth;

namespace NachoClient.iOS
{
    public partial class AccountSettingsViewController : NcUIViewControllerNoLeaks
    {
        protected UIView contentView;
        protected UIScrollView scrollView;

        UIBarButtonItem backButton;
        UIImageView accountImageView;
        UILabel EmailAddress;
        UcNameValuePair DisplayNameTextBlock;
        UcNameValuePair ChangePasswordBlock;
        UcNameValuePair ExpiredPasswordBlock;
        UcNameValuePair RectifyPasswordBlock;
        UcNameValuePair AdvancedSettingsBlock;
        UcNameValuePair SignatureBlock;
        UcNameValuePair DaysToSyncBlock;
        UcNameValuePair NotificationsBlock;
        UISwitch FastNotificationSwitch;
        UISwitch DefaultEmailSwitch;
        UISwitch DefaultCalendarSwitch;
        UIButton FixAccountButton;
        UIButton DeleteAccountButton;
        UIView DeleteAccountBackgroundView;
        UIActivityIndicatorView DeleteAccountActivityIndicator;

        McAccount account;

        static readonly nfloat HEIGHT = 50;
        static readonly nfloat INDENT = 25;

        public void SetAccount (McAccount account)
        {
            this.account = account;
        }

        public AccountSettingsViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
        }

        public override void ViewDidAppear (bool animated)
        {
            if (this.NavigationController.RespondsToSelector (new ObjCRuntime.Selector ("interactivePopGestureRecognizer"))) {
                this.NavigationController.InteractivePopGestureRecognizer.Enabled = true;
                this.NavigationController.InteractivePopGestureRecognizer.Delegate = null;
            }
            base.ViewDidAppear (animated);
        }

        public override void ViewWillDisappear (bool animated)
        {
            View.EndEditing (true);
            base.ViewWillDisappear (animated);
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
        }

        protected override void CreateViewHierarchy ()
        {
            NavigationController.NavigationBar.Translucent = false;
            NavigationItem.Title = "Account Settings";

            backButton = new NcUIBarButtonItem ();
            backButton.Image = UIImage.FromBundle ("nav-backarrow");
            backButton.TintColor = A.Color_NachoBlue;
            backButton.AccessibilityLabel = "Back";
            backButton.Clicked += BackButton_Clicked;

            NavigationItem.SetLeftBarButtonItem (backButton, true);

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            scrollView = new UIScrollView (new CGRect (0, 0, View.Frame.Width, View.Frame.Height));
            scrollView.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
            scrollView.BackgroundColor = A.Color_NachoBackgroundGray;
            scrollView.ScrollEnabled = true;
            scrollView.AlwaysBounceVertical = true;
            scrollView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;
            View.AddSubview (scrollView);

            contentView = new UIView (Util.CardContentRectangle (View.Frame.Width, View.Frame.Height));
            contentView.Layer.CornerRadius = A.Card_Corner_Radius;
            contentView.BackgroundColor = UIColor.White;
            scrollView.AddSubview (contentView);

            accountImageView = new UIImageView (new CGRect (12, 15, 50, 50));
            accountImageView.Layer.CornerRadius = 25;
            accountImageView.Layer.MasksToBounds = true;
            accountImageView.ContentMode = UIViewContentMode.ScaleAspectFill;
            contentView.AddSubview (accountImageView);

            using (var image = Util.ImageForAccount (account)) {
                accountImageView.Image = image;
            }

            EmailAddress = new UILabel (new CGRect (75, 12, contentView.Frame.Width - 75, 50));
            EmailAddress.Text = account.EmailAddr;
            EmailAddress.Font = A.Font_AvenirNextRegular17;
            EmailAddress.TextColor = A.Color_NachoBlack;
            contentView.AddSubview (EmailAddress);

            nfloat yOffset = NMath.Max (accountImageView.Frame.Bottom, EmailAddress.Frame.Bottom);

            Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);

            DisplayNameTextBlock = new UcNameValuePair (new CGRect (0, yOffset, contentView.Frame.Width, HEIGHT), "Description", INDENT, 15, ChangeDescriptionTapHandler);
            contentView.AddSubview (DisplayNameTextBlock);
            yOffset = DisplayNameTextBlock.Frame.Bottom;

            var creds = McCred.QueryByAccountId<McCred> (account.Id).SingleOrDefault ();
            if ((null != creds) && ((McCred.CredTypeEnum.Password == creds.CredType) || (McCred.CredTypeEnum.OAuth2 == creds.CredType))) {
                Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);
                ChangePasswordBlock = new UcNameValuePair (new CGRect (0, yOffset, contentView.Frame.Width, HEIGHT), "Update Password", INDENT, 15, ChangePasswordTapHandler);
                contentView.AddSubview (ChangePasswordBlock);
                yOffset = ChangePasswordBlock.Frame.Bottom;
            }

            if ((McAccount.AccountServiceEnum.Exchange == account.AccountService) || (McAccount.AccountServiceEnum.IMAP_SMTP == account.AccountService)) {
                Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);
                AdvancedSettingsBlock = new UcNameValuePair (new CGRect (0, yOffset, contentView.Frame.Width, HEIGHT), "Advanced Settings", INDENT, 15, AdvancedSettingsTapHandler);
                contentView.AddSubview (AdvancedSettingsBlock);
                yOffset = AdvancedSettingsBlock.Frame.Bottom;
            }
                
            Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);

            SignatureBlock = new UcNameValuePair (new CGRect (0, yOffset, contentView.Frame.Width, HEIGHT), "Signature", INDENT, 15, SignatureTapHandler);
            contentView.AddSubview (SignatureBlock);
            yOffset = SignatureBlock.Frame.Bottom;

            Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);

            DaysToSyncBlock = new UcNameValuePair (new CGRect (0, yOffset, contentView.Frame.Width, HEIGHT), "Days to sync", INDENT, 15, DaysToSyncTapHandler);
            contentView.AddSubview (DaysToSyncBlock);
            yOffset = DaysToSyncBlock.Frame.Bottom;

            Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);

            NotificationsBlock = new UcNameValuePair (new CGRect (0, yOffset, contentView.Frame.Width, HEIGHT), "Notifications", INDENT, 15, NotificationsTapHandler);
            contentView.AddSubview (NotificationsBlock);
            yOffset = NotificationsBlock.Frame.Bottom;

            Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);

            var fastNotificationLabel = new UILabel (new CGRect (INDENT, yOffset, contentView.Frame.Width, HEIGHT));
            fastNotificationLabel.Font = A.Font_AvenirNextRegular14;
            fastNotificationLabel.TextAlignment = UITextAlignment.Left;
            fastNotificationLabel.TextColor = A.Color_NachoDarkText;
            fastNotificationLabel.Text = "Fast Notification";
            fastNotificationLabel.SizeToFit ();
            ViewFramer.Create (fastNotificationLabel).Height (HEIGHT);

            FastNotificationSwitch = new UISwitch ();
            ViewFramer.Create (FastNotificationSwitch).RightAlignX (contentView.Frame.Width - INDENT);
            ViewFramer.Create (FastNotificationSwitch).CenterY (yOffset, HEIGHT);

            FastNotificationSwitch.ValueChanged += FastNotificationSwitchChangedHandler;

            contentView.AddSubview (fastNotificationLabel);
            contentView.AddSubview (FastNotificationSwitch);

            yOffset = fastNotificationLabel.Frame.Bottom;

            if (account.HasCapability (McAccount.AccountCapabilityEnum.EmailSender)) {
                Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);

                var defaultEmailLabel = new UILabel (new CGRect (INDENT, yOffset, contentView.Frame.Width, HEIGHT));
                defaultEmailLabel.Font = A.Font_AvenirNextRegular14;
                defaultEmailLabel.TextAlignment = UITextAlignment.Left;
                defaultEmailLabel.TextColor = A.Color_NachoDarkText;
                defaultEmailLabel.Text = "Default Email Account";
                defaultEmailLabel.SizeToFit ();
                ViewFramer.Create (defaultEmailLabel).Height (HEIGHT);

                DefaultEmailSwitch = new UISwitch ();
                ViewFramer.Create (DefaultEmailSwitch).RightAlignX (contentView.Frame.Width - INDENT);
                ViewFramer.Create (DefaultEmailSwitch).CenterY (yOffset, HEIGHT);

                DefaultEmailSwitch.ValueChanged += DefaultEmailSwitchChangedHandler;

                contentView.AddSubview (defaultEmailLabel);
                contentView.AddSubview (DefaultEmailSwitch);

                yOffset = defaultEmailLabel.Frame.Bottom;
            }

            if (account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter)) {
                Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);

                var defaultCalendarLabel = new UILabel (new CGRect (INDENT, yOffset, contentView.Frame.Width, HEIGHT));
                defaultCalendarLabel.Font = A.Font_AvenirNextRegular14;
                defaultCalendarLabel.TextAlignment = UITextAlignment.Left;
                defaultCalendarLabel.TextColor = A.Color_NachoDarkText;
                defaultCalendarLabel.Text = "Default Calendar Account";
                defaultCalendarLabel.SizeToFit ();
                ViewFramer.Create (defaultCalendarLabel).Height (HEIGHT);

                DefaultCalendarSwitch = new UISwitch ();
                ViewFramer.Create (DefaultCalendarSwitch).RightAlignX (contentView.Frame.Width - INDENT);
                ViewFramer.Create (DefaultCalendarSwitch).CenterY (yOffset, HEIGHT);

                DefaultCalendarSwitch.ValueChanged += DefaultCalendarSwitchChangedHandler;

                contentView.AddSubview (defaultCalendarLabel);
                contentView.AddSubview (DefaultCalendarSwitch);

                yOffset = defaultCalendarLabel.Frame.Bottom;
            }

            var filler1 = new UIView (new CGRect (0, yOffset, contentView.Frame.Width, 20));
            filler1.BackgroundColor = A.Color_NachoBackgroundGray;
            contentView.AddSubview (filler1);
            yOffset = filler1.Frame.Bottom + 5;

            McServer serverWithIssue;
            BackEndStateEnum serverIssue;
            if (LoginHelpers.IsUserInterventionRequired (account.Id, out serverWithIssue, out serverIssue)) {
                FixAccountButton = UIButton.FromType (UIButtonType.System);
                FixAccountButton.Frame = new CGRect (INDENT, yOffset, contentView.Frame.Width, HEIGHT);
                Util.AddButtonImage (FixAccountButton, "gen-avatar-alert", UIControlState.Normal);
                FixAccountButton.TitleEdgeInsets = new UIEdgeInsets (0, 28, 0, 0);
                var serverIssueText = "";
                switch (serverIssue) {
                case BackEndStateEnum.CredWait:
                    serverIssueText = "Update Password";
                    break;
                case BackEndStateEnum.CertAskWait:
                    serverIssueText = "Certificate Issue";
                    break;
                case BackEndStateEnum.ServerConfWait:
                    serverIssueText = "Server Error";
                    break;
                }
                FixAccountButton.SetTitle (serverIssueText, UIControlState.Normal);
                FixAccountButton.AccessibilityLabel = serverIssueText;
                FixAccountButton.Font = A.Font_AvenirNextRegular14;
                FixAccountButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
                FixAccountButton.TouchUpInside += FixAccountButton_TouchUpInside;
                contentView.AddSubview (FixAccountButton);
                yOffset = FixAccountButton.Frame.Bottom;

                var filler2 = new UIView (new CGRect (0, yOffset, contentView.Frame.Width, 20));
                filler2.BackgroundColor = A.Color_NachoBackgroundGray;
                contentView.AddSubview (filler2);
                yOffset = filler2.Frame.Bottom + 5;
            }

            DateTime expiry;
            string rectificationUrl;
            if (LoginHelpers.PasswordWillExpire (account.Id, out expiry, out rectificationUrl)) {
                var expiryText = "Password expires " + Pretty.ReminderDate (expiry);
                ExpiredPasswordBlock = new UcNameValuePair (new CGRect (0, yOffset, contentView.Frame.Width, HEIGHT), expiryText, INDENT, 15, ExpiredPasswordTapHandler);
                contentView.AddSubview (ExpiredPasswordBlock);
                yOffset = ExpiredPasswordBlock.Frame.Bottom;
                if (!String.IsNullOrEmpty (rectificationUrl)) {
                    RectifyPasswordBlock = new UcNameValuePair (new CGRect (0, yOffset, contentView.Frame.Width, HEIGHT), rectificationUrl, INDENT, 15, RectifyPasswordTapHandler);
                    contentView.AddSubview (RectifyPasswordBlock);
                    yOffset = RectifyPasswordBlock.Frame.Bottom;
                }
                var filler3 = new UIView (new CGRect (0, yOffset, contentView.Frame.Width, 20));
                filler3.BackgroundColor = A.Color_NachoBackgroundGray;
                contentView.AddSubview (filler3);
                yOffset = filler3.Frame.Bottom + 5;
            }
                            
            DeleteAccountButton = UIButton.FromType (UIButtonType.System);
            DeleteAccountButton.Frame = new CGRect (INDENT, yOffset, contentView.Frame.Width, HEIGHT);
            Util.AddButtonImage (DeleteAccountButton, "email-delete-two", UIControlState.Normal);
            DeleteAccountButton.TitleEdgeInsets = new UIEdgeInsets (0, 28, 0, 0);
            DeleteAccountButton.SetTitle ("Delete This Account", UIControlState.Normal);
            DeleteAccountButton.AccessibilityLabel = "Delete Account";
            DeleteAccountButton.Font = A.Font_AvenirNextRegular14;
            DeleteAccountButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            DeleteAccountButton.TouchUpInside += onDeleteAccount;
            contentView.AddSubview (DeleteAccountButton);
            yOffset = DeleteAccountButton.Frame.Bottom;

            ViewFramer.Create (contentView).Height (yOffset);

            // Delete Account Spinner - Keeping this separate from the validate credential spinner 
            DeleteAccountBackgroundView = new UIView (new CGRect (0, 0, View.Frame.Width, View.Frame.Height));
            DeleteAccountBackgroundView.BackgroundColor = UIColor.DarkGray.ColorWithAlpha (.6f);
            DeleteAccountBackgroundView.Hidden = true;
            DeleteAccountBackgroundView.Alpha = 0.0f;
            View.AddSubview (DeleteAccountBackgroundView);

            UIView AlertMimicView = new UIView (new CGRect (DeleteAccountBackgroundView.Frame.Width / 2 - 90, DeleteAccountBackgroundView.Frame.Height / 2 - 80, 180, 110));
            AlertMimicView.BackgroundColor = UIColor.White;
            AlertMimicView.Layer.CornerRadius = 6.0f;
            DeleteAccountBackgroundView.AddSubview (AlertMimicView);

            UILabel DeleteAccountStatusMessage = new UILabel (new CGRect (8, 10, AlertMimicView.Frame.Width - 16, 25));
            DeleteAccountStatusMessage.BackgroundColor = UIColor.White;
            DeleteAccountStatusMessage.Alpha = 1.0f;
            DeleteAccountStatusMessage.Font = UIFont.SystemFontOfSize (17);
            DeleteAccountStatusMessage.TextColor = UIColor.Black;
            DeleteAccountStatusMessage.Text = "Deleting Account";
            DeleteAccountStatusMessage.TextAlignment = UITextAlignment.Center;
            AlertMimicView.AddSubview (DeleteAccountStatusMessage);

            DeleteAccountActivityIndicator = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.WhiteLarge);
            DeleteAccountActivityIndicator.Frame = new CGRect (AlertMimicView.Frame.Width / 2 - 20, DeleteAccountStatusMessage.Frame.Bottom + 15, 40, 40);
            DeleteAccountActivityIndicator.Color = A.Color_SystemBlue;
            DeleteAccountActivityIndicator.Alpha = 1.0f;
            DeleteAccountActivityIndicator.StartAnimating ();
            AlertMimicView.AddSubview (DeleteAccountActivityIndicator);
        }

        protected override void ConfigureAndLayout ()
        {
            DisplayNameTextBlock.SetValue (account.DisplayName);

            UpdateSignatureBlock ();

            DaysToSyncBlock.SetValue (Pretty.MaxAgeFilter (account.DaysToSyncEmail));

            FastNotificationSwitch.SetState (account.FastNotificationEnabled, false);

            if (DefaultEmailSwitch != null) {
                var defaultEmailAccount = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.EmailSender);
                bool isDefaultEmail = defaultEmailAccount != null && account.Id == defaultEmailAccount.Id;
                DefaultEmailSwitch.SetState (isDefaultEmail, false);
                DefaultEmailSwitch.Enabled = !isDefaultEmail;
            }

            if (DefaultCalendarSwitch != null) {
                var defaultCalendarAccount = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.CalWriter);
                bool isDefaultCalendar = defaultCalendarAccount != null && account.Id == defaultCalendarAccount.Id;
                DefaultCalendarSwitch.SetState (isDefaultCalendar, false);
                DefaultCalendarSwitch.Enabled = !isDefaultCalendar;
            }

            NotificationsBlock.SetValue (Pretty.NotificationConfiguration (account.NotificationConfiguration));

            var contentViewWidth = contentView.Frame.Width;
            var contentViewHeight = contentView.Frame.Height;
            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
            contentView.Frame = new CGRect (A.Card_Horizontal_Indent, A.Card_Vertical_Indent, contentViewWidth, contentViewHeight);
            scrollView.ContentSize = new CGSize (contentView.Frame.Width + 2 * A.Card_Horizontal_Indent, contentView.Frame.Height + 2 * A.Card_Vertical_Indent);
        }

        void UpdateSignatureBlock ()
        {
            if (!string.IsNullOrEmpty (account.HtmlSignature)) {
                var serializer = new HtmlTextSerializer (account.HtmlSignature);
                var text = serializer.Serialize ();
                SignatureBlock.SetValue (text);
            } else {
                SignatureBlock.SetValue (account.Signature);
            }
        }

        protected override void OnKeyboardChanged ()
        {
            ConfigureAndLayout ();
        }

        protected override void Cleanup ()
        {
            backButton.Clicked -= BackButton_Clicked;

            accountImageView = null;

            if (null != FixAccountButton) {
                FixAccountButton.TouchUpInside -= FixAccountButton_TouchUpInside;
            }
            DeleteAccountButton.TouchUpInside -= onDeleteAccount;
            FastNotificationSwitch.ValueChanged -= FastNotificationSwitchChangedHandler;
            if (DefaultEmailSwitch != null) {
                DefaultEmailSwitch.ValueChanged -= DefaultEmailSwitchChangedHandler;
            }
            if (DefaultCalendarSwitch != null) {
                DefaultCalendarSwitch.ValueChanged -= DefaultCalendarSwitchChangedHandler;
            }
        }

        void BackButton_Clicked (object sender, EventArgs e)
        {
            if (account.Id == NcApplication.Instance.Account.Id) {
                NcApplication.Instance.Account = McAccount.QueryById<McAccount> (account.Id);
            }
            NavigationController.PopViewController (true);
        }

        public bool TextFieldShouldReturn (UITextField whatField)
        {
            View.EndEditing (true);
            return true;
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "SettingsToNotificationChooser") {
                var vc = (NotificationChooserViewController)segue.DestinationViewController;
                vc.Setup (this, account.Id, account.NotificationConfiguration);
                return;
            }
            if (segue.Identifier == "SegueToAdvancedSettings") {
                var vc = (AdvancedSettingsViewController)segue.DestinationViewController;
                vc.Setup (account);
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        void ShowAccountValidation ()
        {
            var vc = new AccountValidationViewController ();
            vc.ChangePassword (account);
            NavigationController.PushViewController (vc, true);
        }

        protected void ChangeDescriptionTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                var descriptionViewController = new SettingsTextPropertyViewController ();
                var tag = "Create a descriptive label for this account.";
                descriptionViewController.Setup ("Description", tag, account.DisplayName, OnSaveDescription);
                NavigationController.PushViewController (descriptionViewController, true);
            }
        }

        protected void ChangePasswordTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                if (!MaybeStartGmailAuth (account)) {
                    ShowAccountValidation ();
                }
            }
        }

        protected void ExpiredPasswordTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                NcActionSheet.Show (ExpiredPasswordBlock, this,
                    new NcAlertAction ("Clear Notification", () => {
                        LoginHelpers.ClearPasswordExpiration (account.Id);
                        ExpiredPasswordBlock.SetLabel ("Password expiration cleared");
                    }),
                    new NcAlertAction ("Cancel", NcAlertActionStyle.Cancel, null)
                );
            }
        }

        protected void RectifyPasswordTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                DateTime expiry;
                string rectificationUrl;
                if (LoginHelpers.PasswordWillExpire (account.Id, out expiry, out rectificationUrl)) {
                    if (!String.IsNullOrEmpty (rectificationUrl)) {
                        var url = new NSUrl (rectificationUrl);
                        if (UIApplication.SharedApplication.CanOpenUrl (url)) {
                            UIApplication.SharedApplication.OpenUrl (url);
                        }
                    }
                }
            }
        }

        protected void AdvancedSettingsTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                PerformSegue ("SegueToAdvancedSettings", this);
            }
        }

        protected void SignatureTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                var signatureController = new SignatureEditViewController ();
                signatureController.Account = account;
                signatureController.OnSave = OnSaveSignature;
                NavigationController.PushViewController (signatureController, true);
            }
        }

        protected void DaysToSyncTapHandler (NSObject sender)
        {
            NcActionSheet.Show (DaysToSyncBlock, this,
                new NcAlertAction (Pretty.MaxAgeFilter (NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.OneMonth_5), () => {
                    UpdateDaysToSync (account.Id, NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.OneMonth_5);
                }),
                new NcAlertAction (Pretty.MaxAgeFilter (NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.SyncAll_0), () => {
                    UpdateDaysToSync (account.Id, NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.SyncAll_0);
                }),
                new NcAlertAction ("Cancel", NcAlertActionStyle.Cancel, null)
            );
        }

        protected void NotificationsTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                PerformSegue ("SettingsToNotificationChooser", this);
            }
        }

        protected void FastNotificationSwitchChangedHandler (object sender, EventArgs e)
        {
            account.FastNotificationEnabled = FastNotificationSwitch.On;
            account.Update ();
            NcApplication.Instance.InvokeStatusIndEventInfo (account, NcResult.SubKindEnum.Info_FastNotificationChanged);
        }

        protected void DefaultEmailSwitchChangedHandler (object sender, EventArgs e)
        {
            var deviceAccount = McAccount.GetDeviceAccount ();
            var mutablesModule = "DefaultAccounts";
            var mutablesKey = String.Format ("Capability.{0}", (int)McAccount.AccountCapabilityEnum.EmailSender);
            McMutables.SetInt (deviceAccount.Id, mutablesModule, mutablesKey, account.Id);
            DefaultEmailSwitch.Enabled = false;
        }

        protected void DefaultCalendarSwitchChangedHandler (object sender, EventArgs e)
        {
            var deviceAccount = McAccount.GetDeviceAccount ();
            var mutablesModule = "DefaultAccounts";
            var mutablesKey = String.Format ("Capability.{0}", (int)McAccount.AccountCapabilityEnum.CalWriter);
            McMutables.SetInt (deviceAccount.Id, mutablesModule, mutablesKey, account.Id);
            DefaultCalendarSwitch.Enabled = false;
        }

        protected void UpdateDaysToSync (int accountId, NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode code)
        {
            DaysToSyncBlock.SetValue (Pretty.MaxAgeFilter (code));
            account.DaysToSyncEmail = code;
            account.Update ();
            NcApplication.Instance.InvokeStatusIndEventInfo (account, NcResult.SubKindEnum.Info_DaysToSyncChanged);
        }

        public void UpdateNotificationConfiguration (int accountId, McAccount.NotificationConfigurationEnum choice)
        {
            NotificationsBlock.SetValue (Pretty.NotificationConfiguration (choice));
            account.NotificationConfiguration = choice;
            account.Update ();
        }

        void OnSaveSignature (SignatureEditViewController signatureController)
        {
            account.Signature = signatureController.EditedPlainSignature;
            account.HtmlSignature = signatureController.EditedHtmlSignature;
            account.Update ();
            UpdateSignatureBlock ();
        }

        void OnSaveDescription (string text)
        {
            DisplayNameTextBlock.SetValue (text);
            account.DisplayName = text;
            account.Update ();
        }

        void FixAccountButton_TouchUpInside (object sender, EventArgs e)
        {
            McServer serverWithIssue;
            BackEndStateEnum serverIssue;
            if (LoginHelpers.IsUserInterventionRequired (account.Id, out serverWithIssue, out serverIssue)) {
                switch (serverIssue) {
                case BackEndStateEnum.CredWait:
                    if (!MaybeStartGmailAuth (account)) {
                        ShowAccountValidation ();
                    }
                    break;
                case BackEndStateEnum.CertAskWait:
                    CertAsk (McAccount.AccountCapabilityEnum.EmailSender);
                    break;
                case BackEndStateEnum.ServerConfWait:
                    if (null == serverWithIssue || !serverWithIssue.IsHardWired) {
                        PerformSegue ("SegueToAdvancedSettings", this);
                    } else {
                        BackEnd.Instance.ServerConfResp (serverWithIssue.AccountId, serverWithIssue.Capabilities, false);
                        NavigationController.PopViewController (true);
                    }
                    break;
                }
            }
        }

        void CertAsk (McAccount.AccountCapabilityEnum capability)
        {
            var vc = new CertAskViewController ();
            vc.Setup (account, capability);
            NavigationController.PushViewController (vc, true);
        }

        void onDeleteAccount (object sender, EventArgs e)
        {
            contentView.Hidden = true;
            backButton.Enabled = false;
            ToggleDeleteAccountSpinnerView ();
            Action action = () => {
                NcAccountHandler.Instance.RemoveAccount (account.Id);
                InvokeOnMainThread (() => {
                    backButton.Enabled = true;
                    ToggleDeleteAccountSpinnerView ();
                    // go back to main screen
                    NcUIRedirector.Instance.GoBackToMainScreen ();  
                });
            };
            NcTask.Run (action, "RemoveAccount");
        }

        void ToggleDeleteAccountSpinnerView ()
        {
            DeleteAccountBackgroundView.Hidden = !DeleteAccountBackgroundView.Hidden;

            if (DeleteAccountBackgroundView.Hidden) {
                DeleteAccountActivityIndicator.StopAnimating ();
                DeleteAccountBackgroundView.Alpha = 0.0f;
            } else {
                UIView.Animate (.15, () => {
                    DeleteAccountBackgroundView.Alpha = 1.0f;
                });
                DeleteAccountActivityIndicator.StartAnimating ();
            }
        }

        bool MaybeStartGmailAuth (McAccount account)
        {
            if (McAccount.AccountServiceEnum.GoogleDefault != account.AccountService) {
                return false;
            }
            var cred = McCred.QueryByAccountId<McCred> (account.Id).SingleOrDefault ();
            if (null == cred) {
                return false;
            }
            if (McCred.CredTypeEnum.OAuth2 != cred.CredType) {
                return false;
            }

            StartGoogleLogin ();

            return true;
        }


        public void StartGoogleLogin ()
        {
            var auth = new GoogleOAuth2Authenticator (account.EmailAddr);

            auth.AllowCancel = true;

            // If authorization succeeds or is canceled, .Completed will be fired.
            auth.Completed += (s, e) => {
                DismissViewController (true, () => {
                    if (!e.IsAuthenticated) {
                        return;
                    }

                    string access_token;
                    e.Account.Properties.TryGetValue ("access_token", out access_token);

                    string refresh_token;
                    e.Account.Properties.TryGetValue ("refresh_token", out refresh_token);

                    string expiresString = "0";
                    uint expirationSecs = 0;
                    if (e.Account.Properties.TryGetValue ("expires_in", out expiresString)) {
                        if (!uint.TryParse (expiresString, out expirationSecs)) {
                            Log.Info (Log.LOG_UI, "StartGoogleLogin: Could not convert expires value {0} to int", expiresString);
                        }
                    }

                    var source = "https://www.googleapis.com/oauth2/v1/userinfo";
                    var url = String.Format ("{0}?access_token={1}", source, access_token);
                    Newtonsoft.Json.Linq.JObject userInfo;
                    try {
                        var userInfoString = new WebClient ().DownloadString (url);
                        userInfo = Newtonsoft.Json.Linq.JObject.Parse (userInfoString);
                    } catch (Exception ex) {
                        Log.Info (Log.LOG_HTTP, "Could not download or parse userInfoString from {0}: {1}", source, ex);
                        NcAlertView.ShowMessage (this, "Settings", string.Format ("Could not download user details. Please try again. ({0})", ex.Message));
                        return;
                    }

                    if (!String.Equals (account.EmailAddr, (string)userInfo ["email"], StringComparison.OrdinalIgnoreCase)) {
                        // Can't change your email address
                        NcAlertView.ShowMessage (this, "Settings", "You may not change your email address.  Create a new account to use a new email address.");
                        return;
                    }

                    var cred = McCred.QueryByAccountId<McCred> (account.Id).SingleOrDefault ();
                    cred.UpdateOauth2 (access_token, refresh_token, expirationSecs);

                    BackEnd.Instance.CredResp (account.Id);

                    var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_McCredPasswordChanged);
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                        Status = result,
                        Account = account,
                    });
                });
            };

            auth.Error += (object sender, AuthenticatorErrorEventArgs e) => {
                DismissViewController (true, () => {
                });
            };

            UIViewController vc = auth.GetUI ();
            this.PresentViewController (vc, true, null);
        }

    }
}
