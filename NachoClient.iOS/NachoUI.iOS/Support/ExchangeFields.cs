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
        UIButton advancedButton;
        UIButton startOverButton;
        UIButton customerSupportButton;


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
        AdvancedLoginViewController.onConnectCallback onConnect;

        public ExchangeFields (McAccount account, Prompt prompt, CGRect rect, AdvancedLoginViewController.onConnectCallback onConnect)
        {
            this.account = account;
            this.onConnect = onConnect;

            showAdvancedSettings = true;

            CreateView (rect);
            UpdatePrompt (prompt);
            Layout ();

            if (null != account) {
                LoadAccount ();
            }
        }

        void CreateView (CGRect rect)
        {
            scrollView = new UIScrollView (rect);
            scrollView.BackgroundColor = A.Color_NachoGreen;
            scrollView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;

            contentView = new UIView (View.Frame);
            contentView.BackgroundColor = A.Color_NachoNowBackground;
            scrollView.AddSubview (contentView);

            nfloat yOffset = 0;

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
            connectButton.AccessibilityLabel = "Connect";
            connectButton.BackgroundColor = A.Color_NachoTeal;
            connectButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            connectButton.SetTitle ("Connect", UIControlState.Normal);
            connectButton.TitleLabel.TextColor = UIColor.White;
            connectButton.TitleLabel.Font = A.Font_AvenirNextDemiBold17;
            connectButton.Layer.CornerRadius = 4f;
            connectButton.Layer.MasksToBounds = true;
            connectButton.TouchUpInside += ConnectButton_TouchUpInside;

            contentView.AddSubview (connectButton);

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

            inputViews.Add (emailView);
            inputViews.Add (serverView);
            inputViews.Add (domainView);
            inputViews.Add (usernameView);
            inputViews.Add (passwordView);
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
            var action = SaveUserSettings ();
            CallOnConnect (action);
        }

        void AdvancedButton_TouchUpInside (object sender, EventArgs e)
        {
            showAdvancedSettings = true;
            Layout ();
        }

        public void Layout ()
        {
            nfloat yOffset = 0;

            ViewFramer.Create (infoLabel).Y (yOffset);
            yOffset = infoLabel.Frame.Bottom + 15;

            ViewFramer.Create (emailView).Y (yOffset);
            yOffset += CELL_HEIGHT;

            ViewFramer.Create (passwordView).Y (yOffset);
            yOffset += CELL_HEIGHT;

            ViewFramer.Create (emailWhiteInset).Y (emailView.Frame.Top + (CELL_HEIGHT / 2));
            yOffset += 20;

            if (showAdvancedSettings) {
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

            if (!showAdvancedSettings) {
                ViewFramer.Create (advancedButton).Y (yOffset);
                yOffset = advancedButton.Frame.Bottom + 20;
            }
            advancedButton.Hidden = showAdvancedSettings;

            ViewFramer.Create (customerSupportButton).Y (yOffset);
            yOffset = customerSupportButton.Frame.Bottom + 20;

            ViewFramer.Create (startOverButton).Y (yOffset);
            yOffset = startOverButton.Frame.Bottom + 20;

            // Padding
            yOffset += 20;

            Util.SetHidden (!showAdvancedSettings, serverView, domainView, usernameView, domainWhiteInset);

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
            case Prompt.BadCredentials:
                infoLabel.Text = "There seems to be a problem with your credentials.";
                infoLabel.TextColor = A.Color_NachoRed;
                break;
            }
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
            passwordView.textField.Text = creds.GetPassword ();

            var server = McServer.QueryByAccountId<McServer> (account.Id).FirstOrDefault ();

            if (null != server) {
                if (null == server.UserSpecifiedServerName) {
                    serverView.textField.Text = server.Host;
                } else {
                    serverView.textField.Text = server.UserSpecifiedServerName;
                }
            }
        }

        /// <summary>
        /// Updates McCred and McAccount from the UI
        /// in both theAccount and the database.
        /// </summary>
        public AdvancedLoginViewController.ConnectCallbackStatusEnum SaveUserSettings ()
        {
            if (!CanUserConnect ()) {
                return AdvancedLoginViewController.ConnectCallbackStatusEnum.ContinueToShowAdvanced;
            }
            var email = emailView.textField.Text.Trim ();
            var password = passwordView.textField.Text;

            // FIXME
            // If account exists and is significantly different from what's entered, then re-create it.

            if (null == account) {
                if (LoginHelpers.AccountExists (email)) {
                    // Already have this one.
                    Log.Info (Log.LOG_UI, "avl: SaveUserSettings existing account: {0}", email);
                    return AdvancedLoginViewController.ConnectCallbackStatusEnum.DuplicateAccount;
                }
                account = NcAccountHandler.Instance.CreateAccount (McAccount.AccountServiceEnum.Exchange, email, password);
            }
            var cred = McCred.QueryByAccountId<McCred> (account.Id).Single ();

            account.EmailAddr = email;
            account.Update ();

            cred.UpdatePassword (password);
            cred.Update ();

            // If the user clears the username, we'll let them start over
            if (String.IsNullOrEmpty (domainView.textField.Text) && String.IsNullOrEmpty (usernameView.textField.Text)) {
                cred.UserSpecifiedUsername = false;
                cred.Username = email;
            } else {
                // Otherwise, we'll use what they've entered
                cred.UserSpecifiedUsername = true;
                cred.Username = McCred.Join (domainView.textField.Text, usernameView.textField.Text);
            }
            cred.Update ();
                
            if (showAdvancedSettings) {
                SaveServerSettings ();
            }

            Log.Info (Log.LOG_UI, "avl: a/c updated {0}/{1} username={2}", account.Id, cred.Id, cred.UserSpecifiedUsername);

            return AdvancedLoginViewController.ConnectCallbackStatusEnum.Connect;
        }

        /// <summary>
        /// Saves the server settings.
        /// </summary>
        private void SaveServerSettings ()
        {
            var server = McServer.QueryByAccountId<McServer> (account.Id).SingleOrDefault ();

            // if there is a server record and the text is empty, delete the server record. Let the user start over.
            if (String.IsNullOrEmpty (serverView.textField.Text)) {
                if (null != server) {
                    server.Delete ();
                    Log.Info (Log.LOG_UI, "avl: delete server {0}", server.BaseUriString ());
                }
                return;
            }

            // did the server came from the back end, we're done
            if (null != server) {
                if (null == server.UserSpecifiedServerName) {
                    if (String.Equals (server.Host, serverView.textField.Text, StringComparison.OrdinalIgnoreCase)) {
                        // FIXME: If the email address changed, delete the server
                        Log.Info (Log.LOG_UI, "avl: user did not enter server name");
                        return;
                    }
                }
            }

            // the user specified the host name, save it
            if (null == server) {
                server = new McServer () { 
                    AccountId = account.Id,
                    Capabilities = McAccount.ActiveSyncCapabilities,
                };
                server.Insert ();
            }
            var temp = new McServer ();
            var result = EmailHelper.ParseServer (ref temp, serverView.textField.Text);
            NcAssert.True (EmailHelper.ParseServerWhyEnum.Success_0 == result);
            temp.Capabilities = McAccount.ActiveSyncCapabilities;
            if (!server.IsSameServer (temp)) {
                server.CopyFrom (temp);
                server.UserSpecifiedServerName = serverView.textField.Text;
                server.UsedBefore = false;
                server.Update ();
                Log.Info (Log.LOG_UI, "avl: update server {0}", server.UserSpecifiedServerName);
            }
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

        void SetRedText (AdvancedTextField field, string text)
        {
            infoLabel.Text = text;
        }

        bool CanUserConnect ()
        {
            if (emailView.IsNullOrEmpty ()) {
                SetRedText (emailView, "Enter an email address");
                return false;
            }
            if (passwordView.IsNullOrEmpty ()) {
                SetRedText (passwordView, "Enter a password");
            }
            string serviceName;
            var emailAddress = emailView.textField.Text;
            if (NcServiceHelper.IsServiceUnsupported (emailAddress, out serviceName)) {
                var nuance = String.Format ("Nacho Mail does not support {0} yet.", serviceName);
                SetRedText (emailView, nuance);
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

