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
    public class ExchangeFields : UIView
    {
        protected nfloat CELL_HEIGHT = 44;

        public bool showAdvanced;

        AdvancedTextField emailView;
        AdvancedTextField serverView;
        AdvancedTextField domainView;
        AdvancedTextField usernameView;
        AdvancedTextField passwordView;

        List<AdvancedTextField> inputViews = new List<AdvancedTextField> ();

        UIView emailWhiteInset;
        UIView domainWhiteInset;

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

        public void HighlightEmailError()
        {
            setTextToRed (new AdvancedTextField[] { emailView });
        }

        public void HighlightServerError()
        {
            setTextToRed (new AdvancedTextField[] { serverView });
        }

        public void HighlightUsernameError()
        {
            setTextToRed (new AdvancedTextField[] { domainView, usernameView });
        }

        public void ClearHighlights()
        {
            setTextToRed (new AdvancedTextField[] { });
        }

        public void HighlightEverything()
        {
            setTextToRed (new AdvancedTextField[] {
                emailView,
                domainView,
                usernameView,
                passwordView
            });
        }

        public ExchangeFields (CGRect rect, AdvancedTextField.EditingChanged EditingChangedCallback) : base(rect)
        {
            CreateView (EditingChangedCallback);
        }

        public void CreateView(AdvancedTextField.EditingChanged EditingChangedCallback)
        {
            nfloat yOffset = 0;

            emailView = new AdvancedTextField ("Email", "joe@bigdog.com", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            emailView.EditingChangedCallback = EditingChangedCallback;
            this.AddSubview (emailView);
            yOffset += CELL_HEIGHT;

            passwordView = new AdvancedTextField ("Password", "******", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT));
            passwordView.EditingChangedCallback = EditingChangedCallback;
            passwordView.textField.SecureTextEntry = true;
            this.AddSubview (passwordView);
            yOffset += CELL_HEIGHT;

            emailWhiteInset = new UIView (new CGRect (0, emailView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            emailWhiteInset.BackgroundColor = UIColor.White;
            this.AddSubview (emailWhiteInset);

            yOffset += 25;

            serverView = new AdvancedTextField ("Server", "Server", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            this.AddSubview (serverView);
            yOffset += CELL_HEIGHT;

            yOffset += 25;

            domainView = new AdvancedTextField ("Domain", "Domain", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            this.AddSubview (domainView);
            yOffset += CELL_HEIGHT;

            usernameView = new AdvancedTextField ("Username", "Username", true, new CGRect (0, yOffset, this.Frame.Width + 1, CELL_HEIGHT), UIKeyboardType.EmailAddress);
            this.AddSubview (usernameView);
            yOffset += CELL_HEIGHT;

            domainWhiteInset = new UIView (new CGRect (0, domainView.Frame.Top + (CELL_HEIGHT / 2), 15, CELL_HEIGHT));
            domainWhiteInset.BackgroundColor = UIColor.White;
            this.AddSubview (domainWhiteInset);

            inputViews.Add (emailView);
            inputViews.Add (serverView);
            inputViews.Add (domainView);
            inputViews.Add (usernameView);
            inputViews.Add (passwordView);
        }

        public void Layout()
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
            if (null != theAccount.Account) {
                theAccount.Server = McServer.QueryByAccountId<McServer> (theAccount.Account.Id).SingleOrDefault ();
                if (null != theAccount.Server) {
                    if (null == theAccount.Server.UserSpecifiedServerName) {
                        serverView.textField.Text = theAccount.Server.Host;
                        Log.Info (Log.LOG_UI, "avl: refresh server {0}", serverView.textField.Text);
                    } else {
                        serverView.textField.Text = theAccount.Server.UserSpecifiedServerName;
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

            if (null != theAccount.Server) {
                if (null == theAccount.Server.UserSpecifiedServerName) {
                    serverView.textField.Text = theAccount.Server.Host;
                } else {
                    serverView.textField.Text = theAccount.Server.UserSpecifiedServerName;
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

            // Update the database
            theAccount.Account.Update ();
            theAccount.Credentials.Update ();
            Log.Info (Log.LOG_UI, "avl: a/c updated {0}/{1} username={2}", theAccount.Account.Id, theAccount.Credentials.Id, theAccount.Credentials.UserSpecifiedUsername);

            SaveServerSettings (ref theAccount);
        }

        /// <summary>
        /// Saves the server settings.
        /// </summary>
        private void SaveServerSettings (ref AdvancedLoginViewController.AccountSettings theAccount)
        {
            // if there is a server record and the text is empty, delete the server record. Let the user start over.
            if (String.IsNullOrEmpty (serverView.textField.Text)) {
                DeleteTheServer (ref theAccount, "user cleared server");
                return;
            }

            // did the server came from the back end, we're done
            if (null != theAccount.Server) {
                if (null == theAccount.Server.UserSpecifiedServerName) {
                    if (String.Equals (theAccount.Server.Host, serverView.textField.Text, StringComparison.OrdinalIgnoreCase)) {
                        Log.Info (Log.LOG_UI, "avl: user did not enter server name");
                        return;
                    }
                }
            }

            // the user specified the host name, save it
            if (null == theAccount.Server) {
                theAccount.Server = new McServer () { 
                    AccountId = theAccount.Account.Id,
                    // FIXME STEVE
                    Capabilities = McAccount.ActiveSyncCapabilities,
                };
                theAccount.Server.Insert ();
            }
            var temp = new McServer ();
            var result = EmailHelper.ParseServer (ref temp, serverView.textField.Text);
            NcAssert.True (EmailHelper.ParseServerWhyEnum.Success_0 == result);
            if (!theAccount.Server.IsSameServer (temp)) {
                theAccount.Server.CopyFrom (temp);
                theAccount.Server.UserSpecifiedServerName = serverView.textField.Text;
                theAccount.Server.UsedBefore = false;
                theAccount.Server.Update ();
                Log.Info (Log.LOG_UI, "avl: update server {0}", theAccount.Server.UserSpecifiedServerName);
            }
        }

        public void DeleteTheServer (ref AdvancedLoginViewController.AccountSettings theAccount, string message)
        {
            if (null != theAccount.Server) {
                if (null == theAccount.Server.UserSpecifiedServerName) {
                    Log.Info (Log.LOG_UI, "avl: delete server {0} {1}", message, theAccount.Server.BaseUriString ());
                } else {
                    Log.Info (Log.LOG_UI, "avl: delete user defined server {0} {1}", message, theAccount.Server.UserSpecifiedServerName);
                }
                theAccount.Server.Delete ();
                theAccount.Server = null;
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

    }
}

