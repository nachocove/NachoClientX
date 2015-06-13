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
    public class ExchangeFields : UIView, ILoginFields
    {
        protected nfloat CELL_HEIGHT = 44;

        AdvancedTextField emailView;
        AdvancedTextField serverView;
        AdvancedTextField domainView;
        AdvancedTextField usernameView;
        AdvancedTextField passwordView;

        UIButton connectButton;
        UIButton advancedButton;

        List<AdvancedTextField> inputViews = new List<AdvancedTextField> ();

        UIView emailWhiteInset;
        UIView domainWhiteInset;

        McServer Server { get; set; }

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
                return serverView.textField.Text;
            }
            set {
                serverView.textField.Text = value;
            }
        }

        public string usernameText {
            get {
                return usernameView.textField.Text;
            }
            set {
                usernameView.textField.Text = value;
            }
        }

        public void HighlightEmailError ()
        {
            setTextToRed (new AdvancedTextField[] { emailView });
        }

        public void HighlightServerConfError ()
        {
            if (serverView.IsNullOrEmpty ()) {
                setTextToRed (new AdvancedTextField[] { emailView });
            } else {
                setTextToRed (new AdvancedTextField[] { serverView });
            }
        }

        public void HighlightUsernameError ()
        {
            setTextToRed (new AdvancedTextField[] { domainView, usernameView });
        }

        public void ClearHighlights ()
        {
            setTextToRed (new AdvancedTextField[] { });
        }

        public void HighlightCredentials ()
        {
            setTextToRed (new AdvancedTextField[] {
                emailView,
                domainView,
                usernameView,
                passwordView
            });
        }

        AdvancedLoginViewController.onConnectCallback onConnect;
        McAccount.AccountServiceEnum service;

        public ExchangeFields (CGRect rect, McAccount.AccountServiceEnum service, AdvancedLoginViewController.onConnectCallback onConnect) : base (rect)
        {
            this.service = service;
            this.onConnect = onConnect;
            CreateView ();
        }

        public McAccount CreateAccount ()
        {
            var account = NcAccountHandler.Instance.CreateAccount (service, emailView.textField.Text, passwordView.textField.Text);
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
            serverView = new AdvancedTextField ("Server", "Server", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            serverView.EditingChangedCallback = MaybeEnableConnect;

            this.AddSubview (serverView);
            yOffset += CELL_HEIGHT;

            yOffset += 25;
            domainView = new AdvancedTextField ("Domain", "Domain", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            domainView.EditingChangedCallback = MaybeEnableConnect;

            this.AddSubview (domainView);
            yOffset += CELL_HEIGHT;
            usernameView = new AdvancedTextField ("Username", "Username", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            usernameView.EditingChangedCallback = MaybeEnableConnect;

            this.AddSubview (usernameView);
            yOffset += CELL_HEIGHT;

            domainWhiteInset = new UIView (new CGRect (0, domainView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            domainWhiteInset.BackgroundColor = UIColor.White;
            this.AddSubview (domainWhiteInset);

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

            inputViews.Add (emailView);
            inputViews.Add (serverView);
            inputViews.Add (domainView);
            inputViews.Add (usernameView);
            inputViews.Add (passwordView);
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

            advancedButton.Hidden = showAdvanced || (McAccount.AccountServiceEnum.Exchange != service);

            if (!advancedButton.Hidden) {
                ViewFramer.Create (advancedButton).Y (yOffset);
                yOffset = advancedButton.Frame.Bottom + 20;
            }

            serverView.Hidden = !showAdvanced;
            domainView.Hidden = !showAdvanced;
            usernameView.Hidden = !showAdvanced;
            domainWhiteInset.Hidden = !showAdvanced;

            ViewFramer.Create (this).Height (yOffset);
        }

        /// <summary>
        /// Refreshs the server.  The server potentially changes
        /// </summary>
        public void RefreshTheServer (ref AdvancedLoginViewController.AccountSettings theAccount)
        {
            MaybeEnableConnect (null);
            if (null != theAccount.Account) {
                // FIXME STEVE
                Server = McServer.QueryByAccountId<McServer> (theAccount.Account.Id).FirstOrDefault ();
                if (null != Server) {
                    if (null == Server.UserSpecifiedServerName) {
                        serverView.textField.Text = Server.Host;
                        Log.Info (Log.LOG_UI, "avl: refresh server {0}", serverView.textField.Text);
                    } else {
                        serverView.textField.Text = Server.UserSpecifiedServerName;
                        Log.Info (Log.LOG_UI, "avl: refresh user defined server {0}", serverView.textField.Text);
                    }
                    return;
                }
            }
            Log.Info (Log.LOG_UI, "avl: refresh no server");
            serverView.textField.Text = "";
        }

        public void RefreshUI (AdvancedLoginViewController.AccountSettings theAccount)
        {
            MaybeEnableConnect (null);
            foreach (var v in inputViews) {
                v.textField.Text = "";
                v.textField.TextColor = UIColor.Black;
            }

            if (null != theAccount.Account) {
                emailView.textField.Text = theAccount.Account.EmailAddr;
            }

            if (null != theAccount.Credentials) {
                if (theAccount.Credentials.UserSpecifiedUsername) {
                    string domain, username;
                    McCred.Split (theAccount.Credentials.Username, out domain, out username);
                    usernameView.textField.Text = username;
                    domainView.textField.Text = domain;

                }
                passwordView.textField.Text = theAccount.Credentials.GetPassword ();
            }

            if (null != Server) {
                if (null == Server.UserSpecifiedServerName) {
                    serverView.textField.Text = Server.Host;
                } else {
                    serverView.textField.Text = Server.UserSpecifiedServerName;
                }
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

            // If the user clears the username, we'll let them start over
            if (String.IsNullOrEmpty (domainView.textField.Text) && String.IsNullOrEmpty (usernameView.textField.Text)) {
                theAccount.Credentials.UserSpecifiedUsername = false;
                theAccount.Credentials.Username = theAccount.Account.EmailAddr;
            } else {
                // Otherwise, we'll use what they've entered
                theAccount.Credentials.UserSpecifiedUsername = true;
                theAccount.Credentials.Username = McCred.Join (domainView.textField.Text, usernameView.textField.Text);
            }
                
            if (showAdvanced) {
                SaveServerSettings (ref theAccount);
            }

            // Update the database
            theAccount.Account.Update ();
            theAccount.Credentials.Update ();
            Log.Info (Log.LOG_UI, "avl: a/c updated {0}/{1} username={2}", theAccount.Account.Id, theAccount.Credentials.Id, theAccount.Credentials.UserSpecifiedUsername);
        }

        /// <summary>
        /// Saves the server settings.
        /// </summary>
        private void SaveServerSettings (ref AdvancedLoginViewController.AccountSettings theAccount)
        {
            // if there is a server record and the text is empty, delete the server record. Let the user start over.
            if (String.IsNullOrEmpty (serverView.textField.Text)) {
                DeleteTheServer ();
                return;
            }

            // did the server came from the back end, we're done
            if (null != Server) {
                if (null == Server.UserSpecifiedServerName) {
                    if (String.Equals (Server.Host, serverView.textField.Text, StringComparison.OrdinalIgnoreCase)) {
                        Log.Info (Log.LOG_UI, "avl: user did not enter server name");
                        return;
                    }
                }
            }

            // the user specified the host name, save it
            if (null == Server) {
                Server = new McServer () { 
                    AccountId = theAccount.Account.Id,
                    Capabilities = McAccount.ActiveSyncCapabilities,
                };
                Server.Insert ();
            }
            var temp = new McServer ();
            var result = EmailHelper.ParseServer (ref temp, serverView.textField.Text);
            NcAssert.True (EmailHelper.ParseServerWhyEnum.Success_0 == result);
            temp.Capabilities = McAccount.ActiveSyncCapabilities;
            if (!Server.IsSameServer (temp)) {
                Server.CopyFrom (temp);
                Server.UserSpecifiedServerName = serverView.textField.Text;
                Server.UsedBefore = false;
                Server.Update ();
                Log.Info (Log.LOG_UI, "avl: update server {0}", Server.UserSpecifiedServerName);
            }
        }

        public void MaybeDeleteTheServer ()
        {
            Log.Info (Log.LOG_UI, "avl: maybe delete server");
            if ((null != Server) && (null == Server.UserSpecifiedServerName)) {
                Log.Info (Log.LOG_UI, "avl: maybe delete server {0}", Server.BaseUriString ());
                DeleteTheServer ();
            }
        }

        void DeleteTheServer ()
        {
            if (null != Server) {
                Log.Info (Log.LOG_UI, "avl: delete server {0}", Server.BaseUriString ());
                Server.Delete ();
                Server = null;
            }
        }

        private void setTextToRed (AdvancedTextField[] whichViews)
        {
            foreach (var textView in inputViews) {
                if (whichViews.Contains (textView)) {
                    textView.textField.TextColor = A.Color_NachoRed;
                } else {
                    textView.textField.TextColor = UIColor.Black;
                }
            }
        }

        public string GetServerConfMessage (AdvancedLoginViewController.AccountSettings theAccount, string messagePrefix)
        {
            string message;
            if (null == Server) {
                message = messagePrefix + " for '" + theAccount.Account.EmailAddr + "'.";
            } else if (null == Server.UserSpecifiedServerName) {
                message = messagePrefix + "'" + Server.Host + "'.";
            } else {
                message = messagePrefix + "'" + Server.UserSpecifiedServerName + "'.";
            }
            return message;
        }

        bool haveEnteredEmailAndPassword ()
        {
            return !(String.IsNullOrEmpty (emailView.textField.Text) || String.IsNullOrEmpty (passwordView.textField.Text));
        }

        public void MaybeEnableConnect (UITextField textField)
        {
            if (!haveEnteredEmailAndPassword ()) {
                connectButton.Enabled = false;
                connectButton.Alpha = .5f;
            } else {
                connectButton.Enabled = true;
                connectButton.Alpha = 1.0f;
            }
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
                nuance = String.Format ("Nacho Mail does not support {0} yet.", serviceName);
                return AdvancedLoginViewController.LoginStatus.InvalidEmailAddress;
            }
            string serverName = serverView.textField.Text;
            if (!String.IsNullOrEmpty (serverName)) {
                if (EmailHelper.ParseServerWhyEnum.Success_0 != EmailHelper.IsValidServer (serverName)) {
                    return AdvancedLoginViewController.LoginStatus.InvalidServerName;
                }
            }
            return AdvancedLoginViewController.LoginStatus.OK;
        }

    }
}

