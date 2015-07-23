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

using Prompt = NachoCore.Utils.LoginProtocolControl.Prompt;

namespace NachoClient.iOS
{
    public class IMapFields : ILoginFields
    {
        protected nfloat CELL_HEIGHT = 44;

        AdvancedTextField emailView;
        AdvancedTextField passwordView;

        AdvancedTextField imapServerView;
        AdvancedTextField imapPortNumberView;
        AdvancedTextField smtpServerView;
        AdvancedTextField smtpPortNumberView;

        UILabel infoLabel;
        UIButton connectButton;
        UIButton advancedButton;
        UIButton startOverButton;
        UIButton customerSupportButton;

        UIScrollView scrollView;
        UIView contentView;

        AdvancedTextField[] basicInputViews;
        AdvancedTextField[] advancedInputViews;

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

        Prompt prompt;
        McAccount account;
        AdvancedLoginViewController.onConnectCallback onConnect;
        AdvancedLoginViewController.onValidateCallback onValidate;

        public IMapFields (McAccount account, Prompt prompt, string initialEmail, string initialPassword, CGRect rect, string buttonText)
        {
            this.account = account;
            this.prompt = prompt;

            showAdvancedSettings = true;
            CreateView (rect, buttonText);
            UpdatePrompt (prompt);
            Layout (rect.Height);

            if (null != account) {
                LoadAccount ();
            } else {
                emailView.textField.Text = initialEmail;
                passwordView.textField.Text = initialPassword;
            }
            MaybeEnableConnect (emailView.textField);
        }

        public IMapFields (McAccount account, Prompt prompt, string initialEmail, string initialPassword, CGRect rect, AdvancedLoginViewController.onConnectCallback onConnect)
            : this (account, prompt, initialEmail, initialPassword, rect, "Connect")
        {
            this.onConnect = onConnect;
        }

        public IMapFields (McAccount account, Prompt prompt, string initialEmail, string initialPassword, CGRect rect, AdvancedLoginViewController.onValidateCallback onValidate)
            : this (account, prompt, initialEmail, initialPassword, rect, "Save")
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
            accountImageView.ContentMode = UIViewContentMode.Center;
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
            contentView.AddSubview (infoLabel);
            yOffset = infoLabel.Frame.Bottom + 15;

            emailView = new AdvancedTextField ("Email", "joe@bigdog.com", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            emailView.EditingChangedCallback = MaybeEnableConnect;
            contentView.AddSubview (emailView);
            yOffset += CELL_HEIGHT;

            passwordView = new AdvancedTextField ("Password", "******", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT));
            passwordView.EditingChangedCallback = MaybeEnableConnect;
            passwordView.textField.SecureTextEntry = true;
            contentView.AddSubview (passwordView);
            yOffset += CELL_HEIGHT;

            emailWhiteInset = new UIView (new CGRect (0, emailView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            emailWhiteInset.BackgroundColor = UIColor.White;
            contentView.AddSubview (emailWhiteInset);

            yOffset += 25;

            imapServerView = new AdvancedTextField ("Server", "imap.domain.com", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            imapServerView.EditingChangedCallback = MaybeEnableConnect;
            contentView.AddSubview (imapServerView);
            yOffset += CELL_HEIGHT;

            imapPortNumberView = new AdvancedTextField ("Port", "993", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.Default);
            imapPortNumberView.textField.Text = "993";
            imapPortNumberView.EditingChangedCallback = MaybeEnableConnect;
            contentView.AddSubview (imapPortNumberView);
            yOffset += CELL_HEIGHT;

            imapWhiteInset = new UIView (new CGRect (0, imapServerView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            imapWhiteInset.BackgroundColor = UIColor.White;
            contentView.AddSubview (imapWhiteInset);

            yOffset += 25;

            smtpServerView = new AdvancedTextField ("Server", "smtp.domain.com", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            smtpServerView.EditingChangedCallback = MaybeEnableConnect;
            contentView.AddSubview (smtpServerView);
            yOffset += CELL_HEIGHT;

            smtpPortNumberView = new AdvancedTextField ("Port", "587", true, new CGRect (0, yOffset, View.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
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

            advancedButton = new UIButton (new CGRect (50, yOffset, View.Frame.Width - 100, 20));
            advancedButton.AccessibilityLabel = "Advanced Sign In";
            advancedButton.BackgroundColor = A.Color_NachoNowBackground;
            advancedButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            advancedButton.SetTitle ("Advanced Sign In", UIControlState.Normal);
            advancedButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            advancedButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            advancedButton.TouchUpInside += AdvancedButton_TouchUpInside;
            contentView.AddSubview (advancedButton);
            yOffset = advancedButton.Frame.Bottom + 20;

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

            startOverButton = new UIButton (new CGRect (50, yOffset, View.Frame.Width - 100, 20));
            startOverButton.AccessibilityLabel = "Start Over";
            startOverButton.BackgroundColor = A.Color_NachoNowBackground;
            startOverButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            startOverButton.SetTitle ("Start Over", UIControlState.Normal);
            startOverButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            startOverButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            startOverButton.TouchUpInside += StartOverButton_TouchUpInside;
            contentView.AddSubview (startOverButton);
            yOffset = startOverButton.Frame.Bottom + 20;

            basicInputViews = new AdvancedTextField[] {
                emailView,
                passwordView,
            };
            advancedInputViews = new AdvancedTextField[] {
                imapServerView,
                imapPortNumberView,
                smtpServerView,
                smtpPortNumberView,
            };
        }

        void CallOnConnect (AdvancedLoginViewController.ConnectCallbackStatusEnum connect)
        {
            if (null != onConnect) {
                onConnect (connect, account, emailView.textField.Text, passwordView.textField.Text);
            }
        }

        void StartOverButton_TouchUpInside (object sender, EventArgs e)
        {
            scrollView.EndEditing (true);
            CallOnConnect (AdvancedLoginViewController.ConnectCallbackStatusEnum.StartOver);
        }

        void CustomerSupportButton_TouchUpInside (object sender, EventArgs e)
        {
            scrollView.EndEditing (true);
            CallOnConnect (AdvancedLoginViewController.ConnectCallbackStatusEnum.Support);
        }

        void ConnectButton_TouchUpInside (object sender, EventArgs e)
        {
            scrollView.EndEditing (true);

            if (Prompt.EditInfo == prompt) {
                Validate ();
            } else {
                var action = SaveUserSettings ();
                CallOnConnect (action);
            }
        }

        void AdvancedButton_TouchUpInside (object sender, EventArgs e)
        {
            showAdvancedSettings = true;
            Layout (scrollView.Frame.Height);
        }

        public void Layout (nfloat height)
        {
            nfloat yOffset = 0;

            var editInfo = (NachoCore.Utils.LoginProtocolControl.Prompt.EditInfo == prompt);

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
                ViewFramer.Create (imapServerView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                ViewFramer.Create (imapPortNumberView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                ViewFramer.Create (imapWhiteInset).Y (imapServerView.Frame.Top + (CELL_HEIGHT / 2));
                yOffset += 20;

                ViewFramer.Create (smtpServerView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                ViewFramer.Create (smtpPortNumberView).Y (yOffset);
                yOffset += CELL_HEIGHT;

                ViewFramer.Create (smtpWhiteInset).Y (smtpServerView.Frame.Top + (CELL_HEIGHT / 2));
                yOffset += 20;
            }

            ViewFramer.Create (connectButton).Y (yOffset);
            yOffset = connectButton.Frame.Bottom + 20;

            if (!showAdvancedSettings) {
                ViewFramer.Create (advancedButton).Y (yOffset);
                yOffset = advancedButton.Frame.Bottom + 20;
            }
            advancedButton.Hidden = showAdvancedSettings;

            ViewFramer.Create (customerSupportButton).Y (yOffset);
            yOffset = customerSupportButton.Frame.Bottom + 20;

            ViewFramer.Create (startOverButton).Y (yOffset);
            yOffset = startOverButton.Frame.Bottom + 20;

            startOverButton.Hidden = editInfo;

            // Padding
            yOffset += 20;

            MaybeEnableConnect (emailView.textField);
            Util.SetHidden (!showAdvancedSettings, imapServerView, imapPortNumberView, imapWhiteInset, smtpServerView, smtpPortNumberView, smtpWhiteInset);

            ViewFramer.Create (scrollView).Height (height);
            scrollView.ContentSize = new CGSize (scrollView.Frame.Width, yOffset);
            ViewFramer.Create (contentView).Height (yOffset);
        }

        void UpdatePrompt (Prompt prompt)
        {
            switch (prompt) {
            case Prompt.EnterInfo:
                infoLabel.Text = "Please fill out the required credentials.";
                infoLabel.TextColor = A.Color_NachoGreen;
                break;
            case Prompt.ServerConf:
                infoLabel.Text = GetServerConfMessage ();
                infoLabel.TextColor = A.Color_NachoRed;
                break;
            case Prompt.CredRequest:
                infoLabel.Text = "There seems to be a problem with your credentials.";
                infoLabel.TextColor = A.Color_NachoRed;
                break;
            }
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
            return "We had a problem find a server.";
        }

        // FIXME: How do we pull a msg from the McServer?
        string GetServerConfMessage (McServer server)
        {
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
            passwordView.textField.Text = creds.GetPassword ();

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

        /// <summary>
        /// Updates McCred and McAccount from the UI
        /// in both theAccount and the database.
        /// </summary>
        public AdvancedLoginViewController.ConnectCallbackStatusEnum SaveUserSettings ()
        {
            if (!CanUserConnect ()) {
                infoLabel.TextColor = UIColor.Red;
                return AdvancedLoginViewController.ConnectCallbackStatusEnum.ContinueToShowAdvanced;
            }
            var email = emailView.textField.Text.Trim ();
            var password = passwordView.textField.Text;

            // TODO: Ask jeff
            // Stop/Start did not recover from 2nd wrong password or wrong username
            if (null != account) {
                NcAccountHandler.Instance.RemoveAccount (account.Id);
                account = null;
            }

            if (null == account) {
                if (LoginHelpers.ConfiguredAccountExists (email)) {
                    // Already have this one.
                    Log.Info (Log.LOG_UI, "avl: SaveUserSettings existing account: {0}", email);
                    return AdvancedLoginViewController.ConnectCallbackStatusEnum.DuplicateAccount;
                }
                account = NcAccountHandler.Instance.CreateAccount (McAccount.AccountServiceEnum.IMAP_SMTP, email, password);
            }
            var cred = McCred.QueryByAccountId<McCred> (account.Id).Single ();

            account.EmailAddr = email;
            account.Update ();

            cred.UpdatePassword (password);
            cred.Update ();

            Log.Info (Log.LOG_UI, "avl: a/c updated {0}/{1} username={2}", account.Id, cred.Id, cred.UserSpecifiedUsername);

            if (showAdvancedSettings) {
                return SaveServerSettings ();
            }

            return AdvancedLoginViewController.ConnectCallbackStatusEnum.Connect;
        }

        /// <summary>
        /// Saves the server settings.
        /// </summary>
        private AdvancedLoginViewController.ConnectCallbackStatusEnum SaveServerSettings ()
        {
            DeleteTheServers ("SaveServerSettings");

            if (FieldsAreEmpty (advancedInputViews)) {
                return AdvancedLoginViewController.ConnectCallbackStatusEnum.ContinueToShowAdvanced;
            }

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
            NcModel.Instance.RunInTransaction (() => {
                imapServer.Insert ();
                smtpServer.Insert ();
            });
            Log.Info (Log.LOG_UI, "avl CreateServersForIMAP: {0}/{1}:{2}/{3}:{4}", account.Id, imapServerName, imapServerPort, smtpServer, smtpServerPort);
            return AdvancedLoginViewController.ConnectCallbackStatusEnum.Connect;
        }

        void DeleteTheServers (string message)
        {
            Log.Info (Log.LOG_UI, "avl delete the server {0}", message);
            // FIXME: Only the email servers should be deleted
            var servers = McServer.QueryByAccountId<McServer> (account.Id);
            foreach (var server in servers) {
                server.Delete ();
                Log.Info (Log.LOG_UI, "avl: delete server {0}", server.BaseUriString ());
            }
        }

        void Validate ()
        {
            if (!CanUserConnect ()) {
                return;
            }

            var cred = new McCred ();
            cred.SetTestPassword (passwordView.textField.Text);          

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

        bool FieldsAreSet (params AdvancedTextField[] fields)
        {
            foreach (var field in fields) {
                if (field.IsNullOrEmpty ()) {
                    return false;
                }
            }
            return true;
        }

        bool FieldsAreEmpty (params AdvancedTextField[] fields)
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
                Complain (serverName, "The server name is required.");
                return false;
            }
            if (!EmailHelper.IsValidHost (serverName.textField.Text)) {
                Complain (serverName, "Invalid server name. Please check that you typed it in correctly.");
                return false;
            }
            if (serverName.textField.Text.Contains (":")) {
                Complain (serverName, "Invalid server name. Scheme or port number is not allowed.");
            }
            int result;
            if (!int.TryParse (portNumber.textField.Text, out result)) {
                Complain (portNumber, "Invalid port number. It must be a number.");
                return false;
            }
            return true;
        }

        void Complain (AdvancedTextField field, string text)
        {
            if (Prompt.EditInfo == prompt) {
                var vc = Util.FindOutermostViewController ();
                NcAlertView.ShowMessage (vc, "Settings", text);
            } else {
                infoLabel.Text = text;
                infoLabel.TextColor = A.Color_NachoRed;
            }
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
            }
            string serviceName;
            var emailAddress = emailView.textField.Text;
            if (NcServiceHelper.IsServiceUnsupported (emailAddress, out serviceName)) {
                var nuance = String.Format ("Nacho Mail does not support {0} yet.", serviceName);
                Complain (emailView, nuance);
                return false;
            }

            // TODO: Allow iMap auto-d
//            if (FieldsAreSet (advancedInputViews) && !FieldsAreEmpty (advancedInputViews)) {
//                Complain (null, "All fields must be filled in.");
//                return false;
//            }

            if (!FieldsAreSet (advancedInputViews)) {
                Complain (null, "All fields must be filled in.");
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

