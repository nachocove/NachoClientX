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

namespace NachoClient.iOS
{
    public class IMapFields : UIView, ILoginFields
    {
        protected nfloat CELL_HEIGHT = 44;

        AdvancedTextField emailView;
        AdvancedTextField passwordView;

        AdvancedTextField imapServerView;
        AdvancedTextField imapPortNumberView;
        AdvancedTextField smtpServerView;
        AdvancedTextField smtpPortNumberView;

        UIButton connectButton;
        UIButton advancedButton;

        //        List<AdvancedTextField> basicInputViews = new List<AdvancedTextField> ();
        //        List<AdvancedTextField> advancedInputViews = new List<AdvancedTextField> ();

        AdvancedTextField[] basicInputViews;
        AdvancedTextField[] advancedInputViews;


        UIView emailWhiteInset;
        UIView imapWhiteInset;
        UIView smtpWhiteInset;

        McServer imapServer { get; set; }

        McServer smtpServer { get; set; }

        public bool showAdvanced {
            get;
            set;
        }

        public UIView View {
            get { return this; }
        }

        public string emailText {
            get {
                return emailView.textField.Text;
            }
            set {
                emailView.textField.Text = value;
            }
        }

        public string passwordText {
            get {
                return passwordView.textField.Text;
            }
            set {
                passwordView.textField.Text = value;
            }
        }

        public string serverText {
            get {
                return smtpServerView.textField.Text;
            }
            set {
                smtpServerView.textField.Text = value;
            }
        }

        public string usernameText {
            get {
                return "";
            }
            set {
                ;
            }
        }

        public void HighlightEmailError ()
        {
            setTextToRed (new AdvancedTextField[] { emailView });
        }

        public void HighlightServerConfError ()
        {
            if (smtpServerView.IsNullOrEmpty ()) {
                HighlightEmailError ();
            } else {
                setTextToRed (new AdvancedTextField[] { smtpServerView });
            }
        }

        public void HighlightUsernameError ()
        {
            setTextToRed (new AdvancedTextField[] { });
        }

        public void ClearHighlights ()
        {
            setTextToRed (new AdvancedTextField[] { });
        }

        public void HighlightCredentials ()
        {
            setTextToRed (basicInputViews);
        }

        AdvancedLoginViewController.onConnectCallback onConnect;
        McAccount.AccountServiceEnum service;

        public IMapFields (CGRect rect, McAccount.AccountServiceEnum service, AdvancedLoginViewController.onConnectCallback onConnect) : base (rect)
        {
            this.service = service;
            this.onConnect = onConnect;
            CreateView ();
        }

        public McAccount CreateAccount ()
        {
            var account = NcAccountHandler.Instance.CreateAccount (service, emailView.textField.Text, passwordView.textField.Text);
            NcAccountHandler.Instance.MaybeCreateServersForIMAP (account, service);
            return account;
        }

        void CreateView ()
        {
            nfloat yOffset = 0;

            emailView = new AdvancedTextField ("Email", "joe@bigdog.com", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            emailView.EditingChangedCallback = MaybeEnableConnect;
            this.AddSubview (emailView);
            yOffset += CELL_HEIGHT;

            passwordView = new AdvancedTextField ("Password", "******", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT));
            passwordView.EditingChangedCallback = MaybeEnableConnect;
            passwordView.textField.SecureTextEntry = true;
            this.AddSubview (passwordView);
            yOffset += CELL_HEIGHT;

            emailWhiteInset = new UIView (new CGRect (0, emailView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            emailWhiteInset.BackgroundColor = UIColor.White;
            this.AddSubview (emailWhiteInset);

            yOffset += 25;

            imapServerView = new AdvancedTextField ("Server", "imap.domain.com", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            imapServerView.EditingChangedCallback = MaybeEnableConnect;
            this.AddSubview (imapServerView);
            yOffset += CELL_HEIGHT;

            imapPortNumberView = new AdvancedTextField ("Port", "993", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.Default);
            imapPortNumberView.EditingChangedCallback = MaybeEnableConnect;
            this.AddSubview (imapPortNumberView);
            yOffset += CELL_HEIGHT;

            imapWhiteInset = new UIView (new CGRect (0, imapServerView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            imapWhiteInset.BackgroundColor = UIColor.White;
            this.AddSubview (imapWhiteInset);

            yOffset += 25;

            smtpServerView = new AdvancedTextField ("Server", "smtp.domain.com", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            smtpServerView.EditingChangedCallback = MaybeEnableConnect;
            this.AddSubview (smtpServerView);
            yOffset += CELL_HEIGHT;

            smtpPortNumberView = new AdvancedTextField ("Port", "465", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            smtpPortNumberView.EditingChangedCallback = MaybeEnableConnect;
            this.AddSubview (smtpPortNumberView);
            yOffset += CELL_HEIGHT;

            smtpWhiteInset = new UIView (new CGRect (0, smtpServerView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            smtpWhiteInset.BackgroundColor = UIColor.White;
            this.AddSubview (smtpWhiteInset);

            connectButton = new UIButton (new CGRect (25, yOffset, View.Frame.Width - 50, 46));
            connectButton.AccessibilityLabel = "Connect";
            connectButton.BackgroundColor = A.Color_NachoTeal;
            connectButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            connectButton.SetTitle ("Connect", UIControlState.Normal);
            connectButton.TitleLabel.TextColor = UIColor.White;
            connectButton.TitleLabel.Font = A.Font_AvenirNextDemiBold17;
            connectButton.Layer.CornerRadius = 4f;
            connectButton.Layer.MasksToBounds = true;
            connectButton.TouchUpInside += ConnectButton_TouchUpInside;
            this.AddSubview (connectButton);

            yOffset = connectButton.Frame.Bottom + 20;

            advancedButton = new UIButton (new CGRect (50, yOffset, View.Frame.Width - 100, 20));
            advancedButton.AccessibilityLabel = "Advanced Sign In";
            advancedButton.BackgroundColor = A.Color_NachoNowBackground;
            advancedButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            advancedButton.SetTitle ("Advanced Sign In", UIControlState.Normal);
            advancedButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            advancedButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            advancedButton.TouchUpInside += (object sender, EventArgs e) => {
                View.EndEditing (true);
                showAdvanced = true;
            };
            this.AddSubview (advancedButton);
            yOffset = advancedButton.Frame.Bottom + 20;

            basicInputViews = new AdvancedTextField[] {
                emailView,
                passwordView,
            };
            advancedInputViews = new AdvancedTextField[] {
                emailView,
                passwordView,
                imapServerView,
                imapPortNumberView,
                smtpServerView,
                smtpPortNumberView,
            };
        }

        void ConnectButton_TouchUpInside (object sender, EventArgs e)
        {
            if (null != onConnect) {
                onConnect ();
            }
        }

        public void Layout ()
        {
            nfloat yOffset = 0;
            ViewFramer.Create (emailView).Y (yOffset);
            yOffset += CELL_HEIGHT;

            ViewFramer.Create (passwordView).Y (yOffset);
            yOffset += CELL_HEIGHT;

            ViewFramer.Create (emailWhiteInset).Y (emailView.Frame.Top + (CELL_HEIGHT / 2));
            yOffset += 20;

            if (showAdvanced) {
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

            advancedButton.Hidden = showAdvanced || (McAccount.AccountServiceEnum.IMAP_SMTP != service);

            if (!advancedButton.Hidden) {
                ViewFramer.Create (advancedButton).Y (yOffset);
                yOffset = advancedButton.Frame.Bottom + 20;
            }

            if (!showAdvanced) {
                ViewFramer.Create (advancedButton).Y (yOffset);
                yOffset = advancedButton.Frame.Bottom + 20;
            }

            imapServerView.Hidden = !showAdvanced;
            imapPortNumberView.Hidden = !showAdvanced;
            imapWhiteInset.Hidden = !showAdvanced;
            smtpServerView.Hidden = !showAdvanced;
            smtpPortNumberView.Hidden = !showAdvanced;
            smtpWhiteInset.Hidden = !showAdvanced;

            ViewFramer.Create (this).Height (yOffset);
        }

        /// <summary>
        /// Refreshs the server.  Do not overwrite user supplied fields.
        /// </summary>
        public void RefreshTheServer (ref AdvancedLoginViewController.AccountSettings theAccount)
        {
            MaybeEnableConnect (null);

            if (null == theAccount.Account) {
                return;
            }
            imapServer = McServer.QueryByAccountIdAndCapabilities (theAccount.Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter);
            if (null != imapServer) {
                imapServerView.textField.Text = imapServer.Host;
                imapPortNumberView.textField.Text = imapServer.Port.ToString ();
                Log.Info (Log.LOG_UI, "avl: refresh imap server {0}:{1}", imapServerView.textField.Text, imapPortNumberView.textField.Text);
            } else {
                Log.Info (Log.LOG_UI, "avl: refresh no imap server");
            }
            smtpServer = McServer.QueryByAccountIdAndCapabilities (theAccount.Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
            if (null != smtpServer) {
                smtpServerView.textField.Text = smtpServer.Host;
                smtpPortNumberView.textField.Text = smtpServer.Port.ToString ();
                Log.Info (Log.LOG_UI, "avl: refresh smtp server {0}:{1}", smtpServerView.textField.Text, smtpPortNumberView.textField.Text);
            } else {
                Log.Info (Log.LOG_UI, "avl: refresh no smtp server");
            }
        }

        public void RefreshUI (AdvancedLoginViewController.AccountSettings theAccount)
        {
            MaybeEnableConnect (null);

            foreach (var v in advancedInputViews) {
                v.textField.Text = "";
                v.textField.TextColor = UIColor.Black;
            }

            if (null != theAccount.Account) {
                emailView.textField.Text = theAccount.Account.EmailAddr;
            }

            if (null != theAccount.Credentials) {
                passwordView.textField.Text = theAccount.Credentials.GetPassword ();
            }

            imapServer = McServer.QueryByAccountIdAndCapabilities (theAccount.Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter);
            if (null != imapServer) {
                imapServerView.textField.Text = imapServer.Host;
                imapPortNumberView.textField.Text = imapServer.Port.ToString ();
            }
            smtpServer = McServer.QueryByAccountIdAndCapabilities (theAccount.Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
            if (null != smtpServer) {
                smtpServerView.textField.Text = smtpServer.Host;
                smtpPortNumberView.textField.Text = smtpServer.Port.ToString ();
            }
        }

        /// <summary>
        /// Updates McCred and McAccount from the UI
        /// in both theAccount and the database.
        /// </summary>
        public void SaveUserSettings (ref AdvancedLoginViewController.AccountSettings theAccount)
        {
            NcAssert.NotNull (theAccount.Account);
            NcAssert.NotNull (theAccount.Credentials);

            // Save email & password
            theAccount.Account.EmailAddr = emailView.textField.Text.Trim ();
            theAccount.Credentials.UpdatePassword (passwordView.textField.Text);

            // Update the database
            theAccount.Account.Update ();
            theAccount.Credentials.Update ();
            Log.Info (Log.LOG_UI, "avl: a/c updated {0}/{1} username={2}", theAccount.Account.Id, theAccount.Credentials.Id, theAccount.Credentials.UserSpecifiedUsername);

            if (showAdvanced) {
                SaveServerSettings (ref theAccount);
            }
        }

        /// <summary>
        /// Saves the server settings.
        /// </summary>
        private void SaveServerSettings (ref AdvancedLoginViewController.AccountSettings theAccount)
        {
            // The user must enter the servers & ports.  If the UI gets
            // here without filled in fields, it's a but & assert fail.

            NcAssert.False (string.IsNullOrEmpty (imapServerView.textField.Text));
            NcAssert.False (string.IsNullOrEmpty (imapPortNumberView.textField.Text));
            NcAssert.False (string.IsNullOrEmpty (smtpServerView.textField.Text));
            NcAssert.False (string.IsNullOrEmpty (smtpPortNumberView.textField.Text));

            DeleteTheServers ("SaveServerSettings");

            var imapServerName = imapServerView.textField.Text;
            var smtpServerName = smtpServerView.textField.Text;

            int imapServerPort;
            var imapPortTryParse = int.TryParse (imapPortNumberView.textField.Text, out imapServerPort);
            NcAssert.True (imapPortTryParse);

            int smtpServerPort;
            var smtpPortTryParse = int.TryParse (smtpPortNumberView.textField.Text, out smtpServerPort);
            NcAssert.True (smtpPortTryParse);

            var imapServer = McServer.Create (theAccount.Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter, imapServerName, imapServerPort);
            var smtpServer = McServer.Create (theAccount.Account.Id, McAccount.AccountCapabilityEnum.EmailSender, smtpServerName, smtpServerPort);
            NcModel.Instance.RunInTransaction (() => {
                imapServer.Insert ();
                smtpServer.Insert ();
            });
            Log.Info (Log.LOG_UI, "avl CreateServersForIMAP: {0}/{1}:{2}/{3}:{4}", theAccount.Account.Id, imapServerName, imapServerPort, smtpServer, smtpServerPort);
        }

        public void MaybeDeleteTheServer ()
        {
            // For iMAP, the user must enter the server.  Servers are recreated when saving the servers.
        }

        void DeleteTheServers (string message)
        {
            Log.Info (Log.LOG_UI, "avl delete the server {0}", message);
            if (null != imapServer) {
                imapServer.Delete ();
                imapServer = null;
            }
            if (null != smtpServer) {
                smtpServer.Delete ();
                smtpServer = null;
            }
        }

        private void setTextToRed (AdvancedTextField[] whichViews)
        {
            foreach (var textView in advancedInputViews) {
                if (whichViews.Contains (textView)) {
                    textView.textField.TextColor = A.Color_NachoRed;
                } else {
                    textView.textField.TextColor = UIColor.Black;
                }
            }
        }

        // FIXME: need capabilties etc etc
        public string GetServerConfMessage (AdvancedLoginViewController.AccountSettings theAccount, string messagePrefix)
        {
            string message;
            if (null == imapServer) {
                message = messagePrefix + " for '" + theAccount.Account.EmailAddr + "'.";
            } else if (null == imapServer.UserSpecifiedServerName) {
                message = messagePrefix + " '" + imapServer.Host + "'.";
            } else {
                message = messagePrefix + " '" + imapServer.UserSpecifiedServerName + "'.";
            }
            return message;
        }

        AdvancedLoginViewController.LoginStatus CheckServer (AdvancedTextField serverName, AdvancedTextField portNumber, bool highlight)
        {
            if (serverName.IsNullOrEmpty ()) {
                if (highlight) {
                    setTextToRed (new AdvancedTextField[] { serverName });
                }
                return AdvancedLoginViewController.LoginStatus.InvalidServerName;
            }
            if (EmailHelper.ParseServerWhyEnum.Success_0 != EmailHelper.IsValidServer (serverName.textField.Text)) {
                if (highlight) {
                    setTextToRed (new AdvancedTextField[] { serverName });
                }
                return AdvancedLoginViewController.LoginStatus.InvalidServerName;
            }
            if (portNumber.IsNullOrEmpty ()) {
                if (highlight) {
                    setTextToRed (new AdvancedTextField[] { portNumber });
                }
                return AdvancedLoginViewController.LoginStatus.InvalidPortNumber;
            }
            int result;
            if (!int.TryParse (portNumber.textField.Text, out result)) {
                if (highlight) {
                    setTextToRed (new AdvancedTextField[] { portNumber });
                }
                return AdvancedLoginViewController.LoginStatus.InvalidPortNumber;
            }
            return AdvancedLoginViewController.LoginStatus.OK;
        }

        bool FieldsAreSet (AdvancedTextField[] fields)
        {
            foreach (var field in fields) {
                if (field.IsNullOrEmpty ()) {
                    return false;
                }
            }
            return true;
        }

        public void MaybeEnableConnect (UITextField textField)
        {
            var enable = (showAdvanced ? FieldsAreSet (advancedInputViews) : FieldsAreSet (basicInputViews));
            connectButton.Enabled = enable;
            connectButton.Alpha = (enable ? 1.0f : .5f);
        }

        public AdvancedLoginViewController.LoginStatus CanUserConnect (out string nuance)
        {
            nuance = "";
            if (emailView.IsNullOrEmpty ()) {
                return AdvancedLoginViewController.LoginStatus.EnterInfo;
            }
            if (passwordView.IsNullOrEmpty ()) {
                return AdvancedLoginViewController.LoginStatus.EnterInfo;
            }
            if (null != McAccount.QueryByEmailAddr(emailView.textField.Text).FirstOrDefault()) {
                nuance = "That email address is already in use. Duplicate accounts are not supported.";
                return AdvancedLoginViewController.LoginStatus.InvalidEmailAddress;
            }
            string serviceName;
            var emailAddress = emailView.textField.Text;
            if (EmailHelper.IsServiceUnsupported (emailAddress, out serviceName)) {
                HighlightEmailError ();
                nuance = String.Format ("Nacho Mail does not support {0} yet.", serviceName);
                return AdvancedLoginViewController.LoginStatus.InvalidEmailAddress;
            }
            if (!showAdvanced) {
                return AdvancedLoginViewController.LoginStatus.OK;
            }
            var imapCheck = CheckServer (imapServerView, imapPortNumberView, true);
            if (AdvancedLoginViewController.LoginStatus.OK != imapCheck) {
                return imapCheck;
            }
            var smtpCheck = CheckServer (smtpServerView, smtpPortNumberView, true);
            if (AdvancedLoginViewController.LoginStatus.OK != smtpCheck) {
                return smtpCheck;
            }
            return AdvancedLoginViewController.LoginStatus.OK;
        }

    }
}

