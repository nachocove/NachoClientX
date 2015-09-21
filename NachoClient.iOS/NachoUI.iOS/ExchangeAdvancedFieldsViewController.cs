// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
using System.Collections.Generic;

using Foundation;
using UIKit;
using CoreGraphics;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class ExchangeAdvancedFieldsViewController : AccountAdvancedFieldsViewController
	{

        private bool LockServerField = false;
        private bool LockDomainField = false;
        private bool LockUsernameField = false;

        public ExchangeAdvancedFieldsViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            var label = FieldLabel ("Username");
            usernameField.LeftViewMode = UITextFieldViewMode.Always;
            usernameField.AdjustedEditingInsets = new UIEdgeInsets (0, label.Frame.Width + 30, 0, 15);
            usernameField.AdjustedLeftViewRect = new CGRect(15, 13, label.Frame.Width, label.Frame.Height);
            usernameField.LeftView = label;

            label = FieldLabel ("Domain");
            domainField.LeftViewMode = UITextFieldViewMode.Always;
            domainField.AdjustedEditingInsets = new UIEdgeInsets (0, label.Frame.Width + 30, 0, 15);
            domainField.AdjustedLeftViewRect = new CGRect(15, 13, label.Frame.Width, label.Frame.Height);
            domainField.LeftView = label;

            label = FieldLabel ("Server");
            serverField.LeftViewMode = UITextFieldViewMode.Always;
            serverField.AdjustedEditingInsets = new UIEdgeInsets (0, label.Frame.Width + 30, 0, 15);
            serverField.AdjustedLeftViewRect = new CGRect(15, 13, label.Frame.Width, label.Frame.Height);
            serverField.LeftView = label;
        }

        private UILabel FieldLabel (String text)
        {
            var label = new UILabel(new CGRect(0, 0, 80, 20));
            label.BackgroundColor = UIColor.Clear;
            label.TextColor = A.Color_NachoGreen;
            label.Font = A.Font_AvenirNextMedium14;
            label.Text = text;
            return label;
        }

        public override bool CanSubmitFields ()
        {
            if (!String.IsNullOrEmpty (domainField.Text)) {
                return !String.IsNullOrEmpty (usernameField.Text);
            }
            return true;    
        }

        public override string IssueWithFields ()
        {
            if (!String.IsNullOrEmpty (serverField.Text)) {
                var result = EmailHelper.IsValidServer (serverField.Text);
                if (EmailHelper.ParseServerWhyEnum.Success_0 != result) {
                    return EmailHelper.ParseServerWhyEnumToString (result);
                }
            }
            return null;
        }

        public override void PopulateAccountWithFields (NachoCore.Model.McAccount account)
        {
            var cred = McCred.QueryByAccountId<McCred> (account.Id).Single ();
            var username = usernameField.Text.Trim ();
            var domain = domainField.Text.Trim ();
            var serverString = serverField.Text.Trim ();
            if (!String.IsNullOrEmpty (username)) {
                cred.UserSpecifiedUsername = !LockUsernameField || (!LockDomainField && !string.IsNullOrEmpty(domain));
                cred.Username = McCred.Join (domain, username);
                cred.Update ();
            } else if (!String.IsNullOrEmpty (domain)) {
                cred.UserSpecifiedUsername = !LockDomainField;
                cred.Username = McCred.Join (domain, username);
                cred.Update ();
            } else {
                cred.UserSpecifiedUsername = false;
                cred.Update ();
            }
            var server = McServer.QueryByAccountId<McServer> (account.Id).FirstOrDefault ();
            if (server != null) {
                if (String.IsNullOrEmpty (serverString)) {
                    Log.Info (Log.LOG_UI, "ExchangeAdvancedFields remove server: {0}/{1}", account.Id, server.Id);
                    server.Delete ();
                    server = null;
                }
            } else {
                if (!String.IsNullOrEmpty (serverString)) {
                    server = new McServer () { 
                        AccountId = account.Id,
                        Capabilities = McAccount.ActiveSyncCapabilities,
                    };
                    server.UsedBefore = false;
                    server.Insert ();
                    Log.Info (Log.LOG_UI, "ExchangeAdvancedFields cerate server: {0}/{1}", account.Id, server.Id);
                }
            }
            if (server != null && !String.IsNullOrEmpty (serverString)) {
                var result = EmailHelper.ParseServer (ref server, serverString);
                NcAssert.True (EmailHelper.ParseServerWhyEnum.Success_0 == result);
                if (!LockServerField) {
                    server.UserSpecifiedServerName = serverString;
                }
                server.Update ();
                Log.Info (Log.LOG_UI, "ExchangeAdvancedFields update server: {0}/{1}/{2}", account.Id, server.Id, serverString);
            }
        }

        public override void UnpopulateAccount (McAccount account)
        {
            if (account != null) {
                var server = McServer.QueryByAccountId<McServer> (account.Id).FirstOrDefault ();
                if (server != null) {
                    Log.Info (Log.LOG_UI, "ExchangeAdvancedFields unpopulate server: {0}/{1}", account.Id, server.Id);
                    server.Delete ();
                }
            }
        }

        public override void PopulateFieldsWithAccount (NachoCore.Model.McAccount account)
        {
            if (account != null) {
                var creds = McCred.QueryByAccountId<McCred> (account.Id).Single ();
                if (creds != null) {
                    string domain, username;
                    McCred.Split (creds.Username, out domain, out username);
                    if (String.IsNullOrEmpty (domain) && !String.IsNullOrEmpty (account.EmailAddr) && String.Equals (username, account.EmailAddr)) {
                        usernameField.Text = "";
                        domainField.Text = "";
                    } else {
                        usernameField.Text = username;
                        domainField.Text = domain;
                    }
                } else {
                    usernameField.Text = "";
                    domainField.Text = "";
                }

                var server = McServer.QueryByAccountId<McServer> (account.Id).FirstOrDefault ();
                if (null != server) {
                    if (null == server.UserSpecifiedServerName) {
                        string serverName = server.Host;
                        if (server.Port != 0) {
                            serverName = String.Format ("{0}:{1}", server.Host, server.Port);
                        }
                        serverField.Text = serverName;
                    } else {
                        serverField.Text = server.UserSpecifiedServerName;
                    }
                } else {
                    serverField.Text = "";
                }
            } else {
                usernameField.Text = "";
                domainField.Text = "";
                serverField.Text = "";
            }
        }

        public override void SetFieldsEnabled (bool enabled)
        {
            if (!LockUsernameField) {
                usernameField.Enabled = enabled;
            }
            if (!LockServerField) {
                serverField.Enabled = enabled;
            }
            if (!LockDomainField) {
                domainField.Enabled = enabled;
            }
        }

        public override void LockFieldsForMDMConfig (NcMdmConfig config)
        {
            if (!String.IsNullOrEmpty (config.Host)) {
                LockServerField = true;
                serverField.Enabled = false;
                serverField.BackgroundColor = serverField.BackgroundColor.ColorWithAlpha (0.6f);
            }
            if (!String.IsNullOrEmpty (config.Username)) {
                LockUsernameField = true;
                usernameField.Enabled = false;
                usernameField.BackgroundColor = usernameField.BackgroundColor.ColorWithAlpha (0.6f);
            }
            if (!String.IsNullOrEmpty (config.Domain)) {
                LockDomainField = true;
                domainField.Enabled = false;
                domainField.BackgroundColor = domainField.BackgroundColor.ColorWithAlpha (0.6f);
            }
        }

        partial void textFieldChanged (Foundation.NSObject sender)
        {
            AccountDelegate.AdvancedFieldsControllerDidChange (this);
        }
	}
}
