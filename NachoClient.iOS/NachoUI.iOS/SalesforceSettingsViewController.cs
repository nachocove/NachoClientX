// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Net;
using System.Linq;
using Foundation;
using UIKit;
using NachoCore.Model;
using CoreGraphics;
using NachoCore;
using NachoCore.SFDC;
using NachoCore.Utils;
using Xamarin.Auth;

namespace NachoClient.iOS
{
    public partial class SalesforceSettingsViewController : NcUIViewControllerNoLeaks
    {
        McAccount account;

        public SalesforceSettingsViewController () : base ()
        {
        }

        public SalesforceSettingsViewController (IntPtr handle) : base (handle)
        {
        }

        public void SetAccount (McAccount account)
        {
            this.account = account;
        }

        public override void ViewWillAppear (bool animated)
        {
            CheckForRefreshStatus ();
            StartListeningForStatusInd ();
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
            StopListeningForStatusInd ();
            base.ViewWillDisappear (animated);
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
        }

        void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd) {
                IsListeningForStatusInd = true;
                NcApplication.Instance.StatusIndEvent += StatusIndCallback;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent -= StatusIndCallback;
                IsListeningForStatusInd = false;
            }
        }

        void StatusIndCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (s.Status.SubKind == NcResult.SubKindEnum.Info_BackEndStateChanged) {
                if (s.Account != null && s.Account.Id == account.Id) {
                    CheckForRefreshStatus ();
                    RequeryContactCount ();
                    ConfigureAndLayout ();
                }
            }
            if (s.Status.SubKind == NcResult.SubKindEnum.Error_SyncFailed) {
                IsRefreshing = false;
                RequeryContactCount ();
                ConfigureAndLayout ();
                NcAlertView.ShowMessage (this, "Contact Fetch Failed", "Sorry, the contact fetch failed.  Please try again");
            }
            if (s.Status.SubKind == NcResult.SubKindEnum.Info_SyncSucceeded) {
                IsRefreshing = false;
                RequeryContactCount ();
                ConfigureAndLayout ();
            }
            if (s.Status.SubKind == NcResult.SubKindEnum.Error_AuthFailBlocked) {
                IsRefreshing = false;
                RequeryContactCount ();
                ConfigureAndLayout ();
                // TODO: auth
            }
        }

        string InfoText ()
        {
            if (ContactCount == 0 && IsRefreshing) {
                return String.Format ("Connected to your Salesforce account\nsyncing contacts...", ContactCount);
            } else {
                return String.Format ("Connected to your Salesforce account\n{0} contacts synced", ContactCount);
            }
        }


        bool IsListeningForStatusInd;
        static readonly nfloat HEIGHT = 50;
        static readonly nfloat INDENT = 25;

        protected UIView contentView;
        protected UIScrollView scrollView;
        UIBarButtonItem backButton;
        UIImageView accountImageView;
        UcNameValuePair ChangePasswordBlock;
        UILabel EmailAddress;
        UISwitch DefaultEmailSwitch;
        UIButton FixAccountButton;
        UIButton DeleteAccountButton;
        UIView DeleteAccountBackgroundView;
        UIActivityIndicatorView DeleteAccountActivityIndicator;
        UISwitch AddBccSwitch;
        UIButton RefreshAccountButton;
        UIActivityIndicatorView RefreshAccountActivityIndicator;
        bool IsRefreshing;
        int ContactCount;
        UILabel InfoLabel;

        protected override void CreateViewHierarchy ()
        {
            RequeryContactCount ();
            NavigationController.NavigationBar.Translucent = false;
            NavigationItem.Title = "Salesforce Settings";

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

            var infoFont = A.Font_AvenirNextRegular17;

            InfoLabel = new UILabel (new CGRect (0.0f, 0.0f, scrollView.Bounds.Width - 2 * A.Card_Horizontal_Indent, infoFont.LineHeight));
            InfoLabel.Font = infoFont;
            InfoLabel.Lines = 0;
            InfoLabel.LineBreakMode = UILineBreakMode.WordWrap;
            InfoLabel.TextAlignment = UITextAlignment.Center;
            InfoLabel.Text = InfoText ();

            scrollView.AddSubview (InfoLabel);

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

            var addBccLabel = new UILabel (new CGRect (INDENT, yOffset, contentView.Frame.Width, HEIGHT));
            addBccLabel.Font = A.Font_AvenirNextRegular14;
            addBccLabel.TextAlignment = UITextAlignment.Left;
            addBccLabel.TextColor = A.Color_NachoDarkText;
            addBccLabel.Text = "Automatically Add Bcc";
            addBccLabel.SizeToFit ();
            ViewFramer.Create (addBccLabel).Height (HEIGHT);

            AddBccSwitch = new UISwitch ();
            ViewFramer.Create (AddBccSwitch).RightAlignX (contentView.Frame.Width - INDENT);
            ViewFramer.Create (AddBccSwitch).CenterY (yOffset, HEIGHT);

            AddBccSwitch.ValueChanged += AddBccSwitch_ValueChanged;

            contentView.AddSubview (addBccLabel);
            contentView.AddSubview (AddBccSwitch);

            yOffset = addBccLabel.Frame.Bottom;

            Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);
            ChangePasswordBlock = new UcNameValuePair (new CGRect (0, yOffset, contentView.Frame.Width, HEIGHT), "Update Password", INDENT, 15, ChangePasswordTapHandler);
            contentView.AddSubview (ChangePasswordBlock);
            yOffset = ChangePasswordBlock.Frame.Bottom;

            Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);

            RefreshAccountButton = UIButton.FromType (UIButtonType.System);
            RefreshAccountButton.Frame = new CGRect (INDENT, yOffset, contentView.Frame.Width, HEIGHT);
            Util.AddButtonImage (RefreshAccountButton, "folder-folder", UIControlState.Normal);
            RefreshAccountButton.TitleEdgeInsets = new UIEdgeInsets (0, 28, 0, 0);
            RefreshAccountButton.SetTitle ("Refresh Contacts", UIControlState.Normal);
            RefreshAccountButton.SetTitle ("Fetching Contacts...", UIControlState.Disabled);
            RefreshAccountButton.AccessibilityLabel = "Refresh Contacts";
            RefreshAccountButton.Font = A.Font_AvenirNextRegular14;
            RefreshAccountButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            RefreshAccountButton.TouchUpInside += RefreshAccountButton_TouchUpInside;
            contentView.AddSubview (RefreshAccountButton);
            yOffset = RefreshAccountButton.Frame.Bottom;

            RefreshAccountActivityIndicator = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
            RefreshAccountActivityIndicator.HidesWhenStopped = true;
            RefreshAccountActivityIndicator.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
            RefreshAccountActivityIndicator.Frame = new CGRect(RefreshAccountButton.Bounds.Width - RefreshAccountActivityIndicator.Frame.Width - INDENT * 2.0f, (RefreshAccountButton.Bounds.Height - RefreshAccountActivityIndicator.Frame.Height) / 2.0f, RefreshAccountActivityIndicator.Frame.Width, RefreshAccountActivityIndicator.Frame.Height);
            RefreshAccountButton.AddSubview (RefreshAccountActivityIndicator);

            ViewFramer.Create (contentView).Height (yOffset);

            Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);

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
                Util.AddHorizontalLine (INDENT, yOffset, contentView.Frame.Width - INDENT, A.Color_NachoBorderGray, contentView);
            }

            DeleteAccountButton = UIButton.FromType (UIButtonType.System);
            DeleteAccountButton.Frame = new CGRect (INDENT, yOffset, contentView.Frame.Width, HEIGHT);
            Util.AddButtonImage (DeleteAccountButton, "email-delete-two", UIControlState.Normal);
            DeleteAccountButton.TitleEdgeInsets = new UIEdgeInsets (0, 28, 0, 0);
            DeleteAccountButton.SetTitle ("Delete This Account", UIControlState.Normal);
            DeleteAccountButton.AccessibilityLabel = "Delete Account";
            DeleteAccountButton.Font = A.Font_AvenirNextRegular14;
            DeleteAccountButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            DeleteAccountButton.TouchUpInside += DeleteAccountButton_TouchUpInside;
            ;
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

            //connectToSalesforceView = new ConnectToSalesforceCell (new CGRect (0, 60, View.Frame.Width, 80), ConnectToSalesforceSelected);
            //View.AddSubview (connectToSalesforceView);           

        }

//        public void ConnectToSalesforceSelected ()
//        {
//            accountStoryboard = UIStoryboard.FromName ("AccountCreation", null);
//            var credentialsViewController = (SalesforceCredentialsViewController)accountStoryboard.InstantiateViewController ("SalesforceCredentialsViewController");
//            credentialsViewController.Service = McAccount.AccountServiceEnum.SalesForce;
//            credentialsViewController.AccountDelegate = this;
//            NavigationController.PushViewController (credentialsViewController, true);
//        }

        void AddBccSwitch_ValueChanged (object sender, EventArgs e)
        {
            SalesForceProtoControl.SetShouldAddBccToEmail (account.Id, AddBccSwitch.On);
        }

        void RefreshAccountButton_TouchUpInside (object sender, EventArgs e)
        {
            if (!IsRefreshing) {
                var result = BackEnd.Instance.SyncContactsCmd (account.Id);
                if (result.isOK ()) {
                    CheckForRefreshStatus ();
                }
                ConfigureAndLayout ();
            }
        }

        void CheckForRefreshStatus ()
        {
            IsRefreshing = false;
            var pendings = McPending.QueryByOperation (account.Id, McPending.Operations.Sync);
            foreach (var pending in pendings) {
                if (pending.State == McPending.StateEnum.Failed) {
                    pending.Delete ();
                } else {
                    IsRefreshing = true;
                }
            }
            var status = BackEnd.Instance.BackEndState (account.Id, SalesForceProtoControl.SalesForceCapabilities);
            bool isServerReady = status == BackEndStateEnum.PostAutoDPostInboxSync;
            bool isServerWaiting = status == BackEndStateEnum.CertAskWait || status == BackEndStateEnum.CredWait || status == BackEndStateEnum.ServerConfWait;
            IsRefreshing = !isServerWaiting && (IsRefreshing || !isServerReady);
        }

        void RequeryContactCount ()
        {
            ContactCount = McContact.CountByAccountId (account.Id);
        }

        void FixAccountButton_TouchUpInside (object sender, EventArgs e)
        {
            McServer serverWithIssue;
            BackEndStateEnum serverIssue;
            if (LoginHelpers.IsUserInterventionRequired (account.Id, out serverWithIssue, out serverIssue)) {
                switch (serverIssue) {
                case BackEndStateEnum.CredWait:
                    // FIXME Go to SalesForce Oauth2 webview
                    break;
                case BackEndStateEnum.CertAskWait:
                case BackEndStateEnum.ServerConfWait:
                    // TODO: Can this ever happen?
                    break;
                }
            }
        }

        void DeleteAccountButton_TouchUpInside (object sender, EventArgs e)
        {
            scrollView.Hidden = true;
            backButton.Enabled = false;
            ToggleDeleteAccountSpinnerView ();
            Action action = () => {
                NcAccountHandler.Instance.RemoveAccount (account.Id);
                InvokeOnMainThread (() => {
                    backButton.Enabled = true;
                    ToggleDeleteAccountSpinnerView ();
                    // go back to settings screen
                    NavigationController.PopViewController (true);
                });
            };
            NcTask.Run (action, "RemoveAccount"); 
        }

        protected void ChangePasswordTapHandler (NSObject sender)
        {
            StartOauthLogin ();
        }


        public void StartOauthLogin ()
        {
            var auth = new SFDCOAuth2Authenticator (account.EmailAddr);
            auth.AllowCancel = true;
            auth.Completed += AuthCompleted;
            auth.Error += AuthError;

            UIViewController vc = auth.GetUI () as UIViewController;
            this.PresentViewController (vc, true, null);
        }

        public void AuthCompleted (object sender, AuthenticatorCompletedEventArgs e)
        {
            DismissViewController (true, () => {

                if (e.IsAuthenticated) {
                    string access_token;
                    e.Account.Properties.TryGetValue ("access_token", out access_token);

                    string refresh_token;
                    e.Account.Properties.TryGetValue ("refresh_token", out refresh_token);

                    uint expireSecs = 7200;

                    string id_url;
                    e.Account.Properties.TryGetValue ("id", out id_url);

                    var url = String.Format (id_url);
                    var client = new WebClient ();
                    client.Headers.Add ("Authorization", String.Format ("Bearer {0}", access_token));
                    var userInfoString = client.DownloadString (url);

                    var userInfo = Newtonsoft.Json.Linq.JObject.Parse (userInfoString);

                    if (!String.Equals (account.EmailAddr, (string)userInfo ["email"], StringComparison.OrdinalIgnoreCase)) {
                        // Can't change your email address
                        NcAlertView.ShowMessage (this, "Settings", "You may not change your email address.  Create a new account to use a new email address.");
                    } else {
                        var cred = McCred.QueryByAccountId<McCred> (account.Id).SingleOrDefault ();
                        cred.UpdateOauth2 (access_token, refresh_token, expireSecs);

                        BackEnd.Instance.CredResp (account.Id);

                        var result = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_McCredPasswordChanged);
                        NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                            Status = result,
                            Account = account,
                        });
                    }
                }
            });
        }

        public void AuthError (object sender, AuthenticatorErrorEventArgs e)
        {
            DismissViewController (true, null);
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

        protected override void ConfigureAndLayout ()
        {
          
            if (DefaultEmailSwitch != null) {
                var defaultEmailAccount = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.EmailSender);
                bool isDefaultEmail = defaultEmailAccount != null && account.Id == defaultEmailAccount.Id;
                DefaultEmailSwitch.SetState (isDefaultEmail, false);
                DefaultEmailSwitch.Enabled = !isDefaultEmail;
            }

            InfoLabel.Text = InfoText ();
            var infoSize = InfoLabel.SizeThatFits (new CGSize(scrollView.Bounds.Width - 2 * A.Card_Horizontal_Indent, 99999999.0f));
            InfoLabel.Frame = new CGRect (A.Card_Horizontal_Indent, A.Card_Vertical_Indent, scrollView.Bounds.Width - 2 * A.Card_Horizontal_Indent, infoSize.Height);

            var contentViewWidth = contentView.Frame.Width;
            var contentViewHeight = contentView.Frame.Height;
            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
            contentView.Frame = new CGRect (A.Card_Horizontal_Indent, InfoLabel.Frame.Y + InfoLabel.Frame.Height + A.Card_Vertical_Indent, contentViewWidth, contentViewHeight);
            scrollView.ContentSize = new CGSize (contentView.Frame.Width + 2 * A.Card_Horizontal_Indent, contentView.Frame.Y + contentView.Frame.Height + A.Card_Vertical_Indent);

            AddBccSwitch.SetState (SalesForceProtoControl.ShouldAddBccToEmail (account.Id), false);

            if (IsRefreshing) {
                RefreshAccountActivityIndicator.StartAnimating ();
                RefreshAccountButton.Enabled = false;
            } else {
                RefreshAccountActivityIndicator.StopAnimating ();
                RefreshAccountButton.Enabled = true;
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
            DeleteAccountButton.TouchUpInside -= DeleteAccountButton_TouchUpInside;
            if (DefaultEmailSwitch != null) {
                DefaultEmailSwitch.ValueChanged -= AddBccSwitch_ValueChanged;
            }
        }

        void BackButton_Clicked (object sender, EventArgs e)
        {
            if (account.Id == NcApplication.Instance.Account.Id) {
                NcApplication.Instance.Account = McAccount.QueryById<McAccount> (account.Id);
            }
            NavigationController.PopViewController (true);
        }

    }
}
