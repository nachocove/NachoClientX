//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Foundation;
using UIKit;
using CoreGraphics;

using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoClient.iOS
{
    public class IMapFields : ILoginFields
    {
        protected nfloat CELL_HEIGHT = 44;

        AdvancedTextField emailView;
        AdvancedTextField passwordView;
        AdvancedTextField usernameView;

        AdvancedTextField imapServerView;
        AdvancedTextField imapPortNumberView;
        AdvancedTextField smtpServerView;
        AdvancedTextField smtpPortNumberView;

        UILabel infoLabel;
        UILabel imapLabel;
        UILabel smtpLabel;
        UIButton connectButton;
        UIButton customerSupportButton;

        UIScrollView scrollView;
        UIView contentView;

        AdvancedTextField [] basicInputViews;
        AdvancedTextField [] advancedInputViews;

        bool showAdvancedSettings;

        UIView emailWhiteInset;
        UIView imapWhiteInset;
        UIView smtpWhiteInset;

        UIImageView accountImageView;
        UILabel accountEmailAddr;

        public bool showAdvanced {
            get;
            set;
        }

        public UIView View {
            get { return scrollView; }
        }

        McAccount account;
        AdvancedSettingsViewController.onValidateCallback onValidate;

        public IMapFields (McAccount account, string initialEmail, string initialPassword, CGRect rect, string buttonText)
        {
            this.account = account;

            showAdvancedSettings = true;
            CreateView (rect, buttonText);
            Layout (rect.Height);

            if (null != account) {
                LoadAccount ();
            } else {
                emailView.textField.Text = initialEmail;
                passwordView.textField.Text = initialPassword;
            }
            MaybeEnableConnect (emailView.textField);
        }

        public IMapFields (McAccount account, string initialEmail, string initialPassword, CGRect rect, AdvancedSettingsViewController.onValidateCallback onValidate)
            : this (account, initialEmail, initialPassword, rect, "Save")
        {
            this.onValidate = onValidate;
        }

        void CreateView (CGRect rect, string buttonText)
        {
            scrollView = new UIScrollView (rect);
            scrollView.BackgroundColor = A.Color_NachoGreen;
            scrollView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;

            contentView = new UIView (View.Frame);
            contentView.BackgroundColor = A.Color_NachoNowBackground;
            scrollView.AddSubview (contentView);

            nfloat yOffset = 0;

            accountImageView = new UIImageView (new CGRect (12, 15, 50, 50));
            accountImageView.Layer.CornerRadius = 25;
            accountImageView.Layer.MasksToBounds = true;
            accountImageView.ContentMode = UIViewContentMode.ScaleAspectFill;
            contentView.AddSubview (accountImageView);

            accountEmailAddr = new UILabel (new CGRect (75, 12, contentView.Frame.Width - 75, 50));
            accountEmailAddr.Font = A.Font_AvenirNextRegular17;
            accountEmailAddr.TextColor = A.Color_NachoBlack;
            contentView.AddSubview (accountEmailAddr);

            if (null != account) {
                using (var image = Util.ImageForAccount (account)) {
                    accountImageView.Image = image;
                }
                accountEmailAddr.Text = account.EmailAddr;
            }

            infoLabel = new UILabel (new CGRect (20, 15, View.Frame.Width - 40, 50));
            infoLabel.Font = A.Font_AvenirNextRegular17;
            infoLabel.BackgroundColor = A.Color_NachoNowBackground;
            infoLabel.TextColor = A.Color_NachoRed;
            infoLabel.Lines = 2;
            infoLabel.TextAlignment = UITextAlignment.Center;
            infoLabel.Text = NSBundle.MainBundle.LocalizedString ("Please fill out the required credentials.", "");
            infoLabel.TextColor = A.Color_NachoGreen;
            contentView.AddSubview (infoLabel);
            yOffset = infoLabel.Frame.Bottom + 15;

            emailView = new AdvancedTextField (NSBundle.MainBundle.LocalizedString ("Email (address)", ""), "joe@bigdog.com", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            emailView.EditingChangedCallback = MaybeEnableConnect;
            contentView.AddSubview (emailView);
            yOffset += CELL_HEIGHT;

            passwordView = new AdvancedTextField (NSBundle.MainBundle.LocalizedString ("Password", ""), "******", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT));
            passwordView.EditingChangedCallback = MaybeEnableConnect;
            passwordView.textField.SecureTextEntry = true;
            contentView.AddSubview (passwordView);
            yOffset += CELL_HEIGHT;

            emailWhiteInset = new UIView (new CGRect (0, emailView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            emailWhiteInset.BackgroundColor = UIColor.White;
            contentView.AddSubview (emailWhiteInset);

            yOffset += 25;

            usernameView = new AdvancedTextField (NSBundle.MainBundle.LocalizedString ("Username", ""), "joe@bigdog.com", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            usernameView.EditingChangedCallback = MaybeEnableConnect;
            contentView.AddSubview (usernameView);
            yOffset += CELL_HEIGHT;

            yOffset += 25;

            imapLabel = new UILabel (new CGRect (15, yOffset, View.Frame.Width - 15, CELL_HEIGHT));
            imapLabel.Font = A.Font_AvenirNextRegular17;
            imapLabel.BackgroundColor = A.Color_NachoNowBackground;
            imapLabel.Text = NSBundle.MainBundle.LocalizedString ("Incoming Mail Server", "");
            contentView.AddSubview (imapLabel);
            yOffset += CELL_HEIGHT;

            imapServerView = new AdvancedTextField (NSBundle.MainBundle.LocalizedString ("Server", ""), "imap.domain.com", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            imapServerView.EditingChangedCallback = MaybeEnableConnect;
            contentView.AddSubview (imapServerView);
            yOffset += CELL_HEIGHT;

            imapPortNumberView = new AdvancedTextField (NSBundle.MainBundle.LocalizedString ("Port", ""), "993", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.Default);
            imapPortNumberView.textField.Text = "993";
            imapPortNumberView.EditingChangedCallback = MaybeEnableConnect;
            contentView.AddSubview (imapPortNumberView);
            yOffset += CELL_HEIGHT;

            imapWhiteInset = new UIView (new CGRect (0, imapServerView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            imapWhiteInset.BackgroundColor = UIColor.White;
            contentView.AddSubview (imapWhiteInset);

            yOffset += 25;

            smtpLabel = new UILabel (new CGRect (15, yOffset, View.Frame.Width - 15, CELL_HEIGHT));
            smtpLabel.Font = A.Font_AvenirNextRegular17;
            smtpLabel.BackgroundColor = A.Color_NachoNowBackground;
            smtpLabel.Text = NSBundle.MainBundle.LocalizedString ("Outgoing Mail Server", "");
            contentView.AddSubview (smtpLabel);
            yOffset += CELL_HEIGHT;

            smtpServerView = new AdvancedTextField (NSBundle.MainBundle.LocalizedString ("Server", ""), "smtp.domain.com", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            smtpServerView.EditingChangedCallback = MaybeEnableConnect;
            contentView.AddSubview (smtpServerView);
            yOffset += CELL_HEIGHT;

            smtpPortNumberView = new AdvancedTextField (NSBundle.MainBundle.LocalizedString ("Port", ""), "587", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            smtpPortNumberView.textField.Text = "587";
            smtpPortNumberView.EditingChangedCallback = MaybeEnableConnect;
            contentView.AddSubview (smtpPortNumberView);
            yOffset += CELL_HEIGHT;

            smtpWhiteInset = new UIView (new CGRect (0, smtpServerView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            smtpWhiteInset.BackgroundColor = UIColor.White;
            contentView.AddSubview (smtpWhiteInset);

            connectButton = new UIButton (new CGRect (25, yOffset, View.Frame.Width - 50, 46));
            connectButton.AccessibilityLabel = buttonText;
            connectButton.BackgroundColor = A.Color_NachoTeal;
            connectButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            connectButton.SetTitle (buttonText, UIControlState.Normal);
            connectButton.TitleLabel.TextColor = UIColor.White;
            connectButton.TitleLabel.Font = A.Font_AvenirNextDemiBold17;
            connectButton.Layer.CornerRadius = 4f;
            connectButton.Layer.MasksToBounds = true;
            connectButton.TouchUpInside += ConnectButton_TouchUpInside;
            contentView.AddSubview (connectButton);

            yOffset = connectButton.Frame.Bottom + 20;

            customerSupportButton = new UIButton (new CGRect (50, yOffset, View.Frame.Width - 100, 20));
            customerSupportButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Customer Support", "");
            customerSupportButton.BackgroundColor = A.Color_NachoNowBackground;
            customerSupportButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            customerSupportButton.SetTitle (NSBundle.MainBundle.LocalizedString ("Customer Support", ""), UIControlState.Normal);
            customerSupportButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            customerSupportButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            customerSupportButton.TouchUpInside += CustomerSupportButton_TouchUpInside;
            contentView.AddSubview (customerSupportButton);
            yOffset = customerSupportButton.Frame.Bottom + 20;

            basicInputViews = new AdvancedTextField [] {
                emailView,
                passwordView,
            };
            advancedInputViews = new AdvancedTextField [] {
                usernameView,
                imapServerView,
                imapPortNumberView,
                smtpServerView,
                smtpPortNumberView,
            };
        }

        void CustomerSupportButton_TouchUpInside (object sender, EventArgs e)
        {
            scrollView.EndEditing (true);
        }

        void ConnectButton_TouchUpInside (object sender, EventArgs e)
        {
            scrollView.EndEditing (true);
            Validate ();
        }

        void AdvancedButton_TouchUpInside (object sender, EventArgs e)
        {
            showAdvancedSettings = true;
            Layout (scrollView.Frame.Height);
        }

        public void Layout (nfloat height)
        {
            nfloat yOffset = 0;

            var editInfo = true;

            if (editInfo) {
                yOffset = NMath.Max (accountImageView.Frame.Bottom, accountEmailAddr.Frame.Bottom);
                yOffset += 15;
            } else {
                ViewFramer.Create (infoLabel).Y (yOffset);
                yOffset = infoLabel.Frame.Bottom + 15;

                ViewFramer.Create (emailView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                ViewFramer.Create (emailWhiteInset).Y (emailView.Frame.Top + (CELL_HEIGHT / 2));
            }

            accountImageView.Hidden = !editInfo;
            accountEmailAddr.Hidden = !editInfo;
            infoLabel.Hidden = editInfo;
            emailView.Hidden = editInfo;
            emailWhiteInset.Hidden = editInfo;

            ViewFramer.Create (passwordView).Y (yOffset);
            yOffset += CELL_HEIGHT;

            if (showAdvancedSettings) {
                yOffset += 20;
                ViewFramer.Create (usernameView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                yOffset += 20;

                imapLabel.SizeToFit ();
                ViewFramer.Create (imapLabel).Y (yOffset);
                yOffset = imapLabel.Frame.Bottom + 5;

                ViewFramer.Create (imapServerView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                ViewFramer.Create (imapPortNumberView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                ViewFramer.Create (imapWhiteInset).Y (imapServerView.Frame.Top + (CELL_HEIGHT / 2));
                yOffset += 20;

                smtpLabel.SizeToFit ();
                ViewFramer.Create (smtpLabel).Y (yOffset);
                yOffset = smtpLabel.Frame.Bottom + 5;

                ViewFramer.Create (smtpServerView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                ViewFramer.Create (smtpPortNumberView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                ViewFramer.Create (smtpWhiteInset).Y (smtpServerView.Frame.Top + (CELL_HEIGHT / 2));
                yOffset += 20;
            }

            ViewFramer.Create (connectButton).Y (yOffset);
            yOffset = connectButton.Frame.Bottom + 20;

            ViewFramer.Create (customerSupportButton).Y (yOffset);
            yOffset = customerSupportButton.Frame.Bottom + 20;

            // Padding
            yOffset += 20;

            MaybeEnableConnect (emailView.textField);
            Util.SetHidden (!showAdvancedSettings, usernameView, imapLabel, imapServerView, imapPortNumberView, imapWhiteInset, smtpLabel, smtpServerView, smtpPortNumberView, smtpWhiteInset);

            ViewFramer.Create (scrollView).Height (height);
            scrollView.ContentSize = new CGSize (scrollView.Frame.Width, yOffset);
            ViewFramer.Create (contentView).Height (yOffset);
        }

        string GetServerConfMessage ()
        {
            var servers = McServer.QueryByAccountId<McServer> (account.Id);

            foreach (var server in servers) {
                var message = GetServerConfMessage (server);
                if (null != message) {
                    return message;
                }
            }
            return NSBundle.MainBundle.LocalizedString ("We had a problem finding a server.", "");
        }

        // FIXME: How do we pull a msg from the McServer?
        string GetServerConfMessage (McServer server)
        {
            if (null == server) {
                return string.Format (NSBundle.MainBundle.LocalizedString ("We had a problem finding the server for {0}.", ""), account.EmailAddr);
            } else if (null == server.UserSpecifiedServerName) {
                return string.Format (NSBundle.MainBundle.LocalizedString ("We had a problem finding the server '{0}'.", ""), server.Host);
            } else {
                return string.Format (NSBundle.MainBundle.LocalizedString ("We had a problem finding the server '{0}'.", ""), server.UserSpecifiedServerName);
            }
        }

        void LoadAccount ()
        {
            NcAssert.NotNull (account);

            var creds = McCred.QueryByAccountId<McCred> (account.Id).Single ();
            NcAssert.NotNull (creds);

            emailView.textField.Text = account.EmailAddr;
            try {
                passwordView.textField.Text = creds.GetPassword ();
            } catch (KeychainItemNotFoundException ex) {
                Log.Error (Log.LOG_UI, "Imap LoadAccount: KeychainItemNotFoundException {0}", ex.Message);
            }

            usernameView.textField.Text = creds.Username;

            var imapServer = McServer.QueryByAccountIdAndCapabilities (account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter);
            if (null != imapServer) {
                imapServerView.textField.Text = imapServer.Host;
                imapPortNumberView.textField.Text = imapServer.Port.ToString ();
            }
            var smtpServer = McServer.QueryByAccountIdAndCapabilities (account.Id, McAccount.AccountCapabilityEnum.EmailSender);
            if (null != smtpServer) {
                smtpServerView.textField.Text = smtpServer.Host;
                smtpPortNumberView.textField.Text = smtpServer.Port.ToString ();
            }
        }

        void Validate ()
        {
            if (!CanUserConnect ()) {
                return;
            }

            var cred = new McCred ();
            cred.SetTestPassword (passwordView.textField.Text);
            cred.Username = usernameView.textField.Text;

            var imapServerName = imapServerView.textField.Text;
            var smtpServerName = smtpServerView.textField.Text;

            int imapServerPort;
            var imapPortTryParse = int.TryParse (imapPortNumberView.textField.Text, out imapServerPort);
            NcAssert.True (imapPortTryParse);

            int smtpServerPort;
            var smtpPortTryParse = int.TryParse (smtpPortNumberView.textField.Text, out smtpServerPort);
            NcAssert.True (smtpPortTryParse);

            var imapServer = McServer.Create (account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter, imapServerName, imapServerPort);
            var smtpServer = McServer.Create (account.Id, McAccount.AccountCapabilityEnum.EmailSender, smtpServerName, smtpServerPort);

            onValidate (cred, new List<McServer> () { imapServer, smtpServer });
        }

        public void Validated (McCred verifiedCred, List<McServer> verifiedServers)
        {
            var creds = McCred.QueryByAccountId<McCred> (account.Id).First ();
            creds.Username = verifiedCred.Username;
            creds.UserSpecifiedUsername = verifiedCred.UserSpecifiedUsername;
            creds.UpdatePassword (verifiedCred.GetTestPassword ());
            creds.Update ();

            UpdateServer (verifiedServers.ElementAt (0));
            UpdateServer (verifiedServers.ElementAt (1));
        }

        void UpdateServer (McServer verifiedServer)
        {
            var server = McServer.QueryByAccountIdAndCapabilities (account.Id, verifiedServer.Capabilities);
            server.CopyNameFrom (verifiedServer);
            server.Update ();
        }

        bool FieldsAreSet (params AdvancedTextField [] fields)
        {
            foreach (var field in fields) {
                if (field.IsNullOrEmpty ()) {
                    return false;
                }
            }
            return true;
        }

        bool FieldsAreEmpty (params AdvancedTextField [] fields)
        {
            foreach (var field in fields) {
                if (!field.IsNullOrEmpty ()) {
                    return false;
                }
            }
            return true;
        }

        public void MaybeEnableConnect (UITextField textField)
        {
            textField.TextColor = UIColor.Black;

            var enable = FieldsAreSet (basicInputViews);
            enable &= (showAdvancedSettings ? FieldsAreSet (advancedInputViews) : FieldsAreEmpty (advancedInputViews));
            connectButton.Enabled = enable;
            connectButton.Alpha = (enable ? 1.0f : .5f);
        }

        bool CheckServer (AdvancedTextField serverName, AdvancedTextField portNumber, bool highlight)
        {
            if (serverName.IsNullOrEmpty ()) {
                Complain (serverName, NSBundle.MainBundle.LocalizedString ("The server name is required.", ""));
                return false;
            }
            if (!EmailHelper.IsValidHost (serverName.textField.Text)) {
                Complain (serverName, NSBundle.MainBundle.LocalizedString ("Invalid server name. Please check that you typed it in correctly.", ""));
                return false;
            }
            if (serverName.textField.Text.Contains (":")) {
                Complain (serverName, NSBundle.MainBundle.LocalizedString ("Invalid server name. Scheme or port number is not allowed.", ""));
            }
            int result;
            if (!int.TryParse (portNumber.textField.Text, out result)) {
                Complain (portNumber, NSBundle.MainBundle.LocalizedString ("Invalid port number. It must be a number.", ""));
                return false;
            }
            return true;
        }

        void Complain (AdvancedTextField field, string text)
        {
            var vc = Util.FindOutermostViewController ();
            NcAlertView.ShowMessage (vc, NSBundle.MainBundle.LocalizedString ("Settings (title)", ""), text);
            if (null != field) {
                field.textField.TextColor = A.Color_NachoRed;
            }
        }

        bool CanUserConnect ()
        {
            if (emailView.IsNullOrEmpty ()) {
                Complain (emailView, NSBundle.MainBundle.LocalizedString ("Enter an email address", ""));
                return false;
            }
            if (!EmailHelper.IsValidEmail (emailView.textField.Text)) {
                Complain (emailView, NSBundle.MainBundle.LocalizedString ("Email address is invalid", ""));
                return false;
            }
            if (passwordView.IsNullOrEmpty ()) {
                Complain (passwordView, NSBundle.MainBundle.LocalizedString ("Enter a password", ""));
            }
            string serviceName;
            var emailAddress = emailView.textField.Text;
            if (NcServiceHelper.IsServiceUnsupported (emailAddress, out serviceName)) {
                var nuance = String.Format (NSBundle.MainBundle.LocalizedString ("Nacho Mail does not support {0} yet.", ""), serviceName);
                Complain (emailView, nuance);
                return false;
            }

            // TODO: Allow iMap auto-d
            //            if (FieldsAreSet (advancedInputViews) && !FieldsAreEmpty (advancedInputViews)) {
            //                Complain (null, "All fields must be filled in.");
            //                return false;
            //            }

            if (!FieldsAreSet (advancedInputViews)) {
                Complain (null, NSBundle.MainBundle.LocalizedString ("All fields must be filled in.", ""));
                return false;
            }

            var imapCheck = CheckServer (imapServerView, imapPortNumberView, true);
            if (!imapCheck) {
                return imapCheck;
            }
            var smtpCheck = CheckServer (smtpServerView, smtpPortNumberView, true);
            if (!smtpCheck) {
                return smtpCheck;
            }
            return true;
        }

    }
}

