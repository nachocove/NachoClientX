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
    public class ExchangeFields : ILoginFields
    {
        protected nfloat CELL_HEIGHT = 44;

        AdvancedTextField emailView;
        AdvancedTextField serverView;
        AdvancedTextField domainView;
        AdvancedTextField usernameView;
        AdvancedTextField passwordView;

        UILabel infoLabel;
        UIButton connectButton;
        UIButton customerSupportButton;

        UIImageView accountImageView;
        UILabel accountEmailAddr;

        UIScrollView scrollView;
        UIView contentView;

        List<AdvancedTextField> inputViews = new List<AdvancedTextField> ();

        UIView emailWhiteInset;
        UIView domainWhiteInset;

        bool showAdvancedSettings;

        public bool showAdvanced {
            get;
            set;
        }

        public UIView View {
            get { return scrollView; }
        }

        McAccount account;
        AdvancedSettingsViewController.onValidateCallback onValidate;

        private ExchangeFields (McAccount account, string initialEmail, string initialPassword, CGRect rect, string buttonText)
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

        public ExchangeFields (McAccount account, string initialEmail, string initialPassword, CGRect rect, AdvancedSettingsViewController.onValidateCallback onValidate)
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

            // For edit prompt
            accountImageView = new UIImageView (new CGRect (12, 15, 50, 50));
            accountImageView.Layer.CornerRadius = 25;
            accountImageView.Layer.MasksToBounds = true;
            accountImageView.ContentMode = UIViewContentMode.ScaleAspectFill;
            contentView.AddSubview (accountImageView);

            // For edit prompt
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

            // For non-edit prompt
            infoLabel = new UILabel (new CGRect (20, 15, View.Frame.Width - 40, 50));
            infoLabel.Font = A.Font_AvenirNextRegular17;
            infoLabel.BackgroundColor = A.Color_NachoNowBackground;
            infoLabel.TextColor = A.Color_NachoRed;
            infoLabel.Lines = 2;
            infoLabel.TextAlignment = UITextAlignment.Center;
            infoLabel.Text = "Please fill out the required credentials.";
            infoLabel.TextColor = A.Color_NachoGreen;
            contentView.AddSubview (infoLabel);
            yOffset = infoLabel.Frame.Bottom + 15;

            // For non-edit prompt
            emailView = new AdvancedTextField ("Email", "joe@bigdog.com", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            emailView.EditingChangedCallback = MaybeEnableConnect;
            contentView.AddSubview (emailView);

            yOffset = NMath.Max (accountImageView.Frame.Bottom, accountEmailAddr.Frame.Bottom);

            passwordView = new AdvancedTextField ("Password", "******", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT));
            passwordView.EditingChangedCallback = MaybeEnableConnect;
            passwordView.textField.SecureTextEntry = true;
            contentView.AddSubview (passwordView);
            yOffset += CELL_HEIGHT;

            emailWhiteInset = new UIView (new CGRect (0, emailView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            emailWhiteInset.BackgroundColor = UIColor.White;
            contentView.AddSubview (emailWhiteInset);

            yOffset += 25;
            serverView = new AdvancedTextField ("Server", "Server", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            serverView.EditingChangedCallback = MaybeEnableConnect;

            contentView.AddSubview (serverView);
            yOffset += CELL_HEIGHT;

            yOffset += 25;
            domainView = new AdvancedTextField ("Domain", "Domain", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            domainView.EditingChangedCallback = MaybeEnableConnect;

            contentView.AddSubview (domainView);
            yOffset += CELL_HEIGHT;
            usernameView = new AdvancedTextField ("Username", "Username", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            usernameView.EditingChangedCallback = MaybeEnableConnect;

            contentView.AddSubview (usernameView);
            yOffset += CELL_HEIGHT;

            domainWhiteInset = new UIView (new CGRect (0, domainView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            domainWhiteInset.BackgroundColor = UIColor.White;
            contentView.AddSubview (domainWhiteInset);

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

            customerSupportButton = new UIButton (new CGRect (50, yOffset, View.Frame.Width - 100, 20));
            customerSupportButton.AccessibilityLabel = "Customer Support";
            customerSupportButton.BackgroundColor = A.Color_NachoNowBackground;
            customerSupportButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            customerSupportButton.SetTitle ("Customer Support", UIControlState.Normal);
            customerSupportButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            customerSupportButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            customerSupportButton.TouchUpInside += CustomerSupportButton_TouchUpInside;
            contentView.AddSubview (customerSupportButton);
            yOffset = customerSupportButton.Frame.Bottom + 20;

            inputViews.Add (emailView);
            inputViews.Add (serverView);
            inputViews.Add (domainView);
            inputViews.Add (usernameView);
            inputViews.Add (passwordView);
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

                ViewFramer.Create (serverView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                yOffset += 20;

                ViewFramer.Create (domainView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                ViewFramer.Create (usernameView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                ViewFramer.Create (domainWhiteInset).Y (domainView.Frame.Top + (CELL_HEIGHT / 2));
                yOffset += 20;
            }

            ViewFramer.Create (connectButton).Y (yOffset);
            yOffset = connectButton.Frame.Bottom + 20;

            ViewFramer.Create (customerSupportButton).Y (yOffset);
            yOffset = customerSupportButton.Frame.Bottom + 20;

            // Padding
            yOffset += 20;

            MaybeEnableConnect (emailView.textField);
            Util.SetHidden (!showAdvancedSettings, serverView, domainView, usernameView, domainWhiteInset);

            ViewFramer.Create (scrollView).Height (height);
            scrollView.ContentSize = new CGSize (scrollView.Frame.Width, yOffset);
            ViewFramer.Create (contentView).Height (yOffset);
        }

        string GetServerConfMessage ()
        {
            var server = McServer.QueryByAccountId<McServer> (account.Id).SingleOrDefault ();

            string message;
            string messagePrefix = "We had a problem finding the server";

            if (null == server) {
                message = messagePrefix + " for '" + account.EmailAddr + "'.";
            } else if (null == server.UserSpecifiedServerName) {
                message = messagePrefix + " '" + server.Host + "'.";
            } else {
                message = messagePrefix + " '" + server.UserSpecifiedServerName + "'.";
            }
            return message;
        }

        void LoadAccount ()
        {
            NcAssert.NotNull (account);

            var creds = McCred.QueryByAccountId<McCred> (account.Id).Single ();
            NcAssert.NotNull (creds);

            emailView.textField.Text = account.EmailAddr;

            if (creds.UserSpecifiedUsername) {
                string domain, username;
                McCred.Split (creds.Username, out domain, out username);
                usernameView.textField.Text = username;
                domainView.textField.Text = domain;
            }
            try {
                passwordView.textField.Text = creds.GetPassword ();
            } catch (KeychainItemNotFoundException ex) {
                Log.Error (Log.LOG_UI, "Exch LoadAccount: KeychainItemNotFoundException {0}", ex.Message);
            }

            var server = McServer.QueryByAccountId<McServer> (account.Id).FirstOrDefault ();

            if (null != server) {
                if (null == server.UserSpecifiedServerName) {
                    serverView.textField.Text = server.Host;
                } else {
                    serverView.textField.Text = server.UserSpecifiedServerName;
                }
            }
        }

        void Validate ()
        {
            if (!CanUserConnect ()) {
                return;
            }
            var cred = new McCred ();
            cred.SetTestPassword (passwordView.textField.Text);          
            if (String.IsNullOrEmpty (domainView.textField.Text) && String.IsNullOrEmpty (usernameView.textField.Text)) {
                cred.UserSpecifiedUsername = false;
                cred.Username = emailView.textField.Text;
            } else {
                cred.UserSpecifiedUsername = true;
                cred.Username = McCred.Join (domainView.textField.Text, usernameView.textField.Text);
            }
            var parsedServer = new McServer ();
            NcAssert.True (EmailHelper.ParseServerWhyEnum.Success_0 == EmailHelper.ParseServer (ref parsedServer, serverView.textField.Text));

            var server = McServer.QueryByAccountId<McServer> (account.Id).SingleOrDefault ();
            server.CopyNameFrom (parsedServer);

            onValidate (cred, new List<McServer> () { server });
        }

        public void Validated (McCred verifiedCred, List<McServer> verifiedServers)
        {
            var creds = McCred.QueryByAccountId<McCred> (account.Id).First ();
            creds.Username = verifiedCred.Username;
            creds.UserSpecifiedUsername = verifiedCred.UserSpecifiedUsername;
            creds.UpdatePassword (verifiedCred.GetTestPassword ());
            creds.Update ();

            var verifiedServer = verifiedServers.First ();
            var server = McServer.QueryByAccountId<McServer> (account.Id).SingleOrDefault ();
            server.CopyNameFrom (verifiedServer);
            server.Update ();
        }

        bool haveFilledRequiredFields ()
        {
            if (emailView.IsNullOrEmpty ()) {
                return false;
            }
            if (passwordView.IsNullOrEmpty ()) {
                return false;
            }
            if (serverView.IsNullOrEmpty ()) {
                return false;
            }
            return true;
        }

        public void MaybeEnableConnect (UITextField textField)
        {
            textField.TextColor = UIColor.Black;

            if (!haveFilledRequiredFields ()) {
                connectButton.Enabled = false;
                connectButton.Alpha = .5f;
            } else {
                connectButton.Enabled = true;
                connectButton.Alpha = 1.0f;
            }
        }

        void Complain (AdvancedTextField field, string text)
        {
            var vc = Util.FindOutermostViewController ();
            NcAlertView.ShowMessage (vc, "Settings", text);
            if (null != field) {
                field.textField.TextColor = A.Color_NachoRed;
            }
        }

        bool CanUserConnect ()
        {
            if (emailView.IsNullOrEmpty ()) {
                Complain (emailView, "Enter an email address");
                return false;
            }
            if (!EmailHelper.IsValidEmail (emailView.textField.Text)) {
                Complain (emailView, "Email address is invalid");
                return false;
            }
            if (passwordView.IsNullOrEmpty ()) {
                Complain (passwordView, "Enter a password");
                return false;
            }
            if (serverView.IsNullOrEmpty ()) {
                Complain (serverView, "Enter a server");
                return false;
            }
            if (!serverView.IsNullOrEmpty ()) {
                var result = EmailHelper.IsValidServer (serverView.textField.Text);
                if (EmailHelper.ParseServerWhyEnum.Success_0 != result) {
                    Complain (serverView, EmailHelper.ParseServerWhyEnumToString (result));
                    return false;
                }
            }
            string serviceName;
            var emailAddress = emailView.textField.Text;
            if (NcServiceHelper.IsServiceUnsupported (emailAddress, out serviceName)) {
                var nuance = String.Format ("Nacho Mail does not support {0} yet.", serviceName);
                Complain (emailView, nuance);
                return false;
            }
            return true;
        }


        //
        //            // If only password has changed & backend is in CredWait, do cred resp
        //            if (!freshAccount) {
        //                if (!String.Equals (gOriginalPassword, loginFields.passwordText, StringComparison.Ordinal)) {
        //                    Log.Info (Log.LOG_UI, "avl: onConnect retry password");
        //                    // FIXME STEVE
        //                    BackEndStateEnum backEndState = BackEnd.Instance.BackEndState (theAccount.Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
        //                    if (BackEndStateEnum.CredWait == backEndState) {
        //                        BackEnd.Instance.CredResp (theAccount.Account.Id);
        //                        waitScreen.ShowView ("Verifying Your Credentials...");
        //                        return;
        //                    }
        //                }
        //            }

    }
}

