
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore;

namespace NachoClient.AndroidClient
{
    public class AdvancedExchangeView : AccountAdvancedFieldsViewController
    {
        EditText serverField;
        EditText domainField;
        EditText usernameField;

        private bool LockServerField = false;
        private bool LockDomainField = false;
        private bool LockUsernameField = false;

        public AdvancedExchangeView(View view) : base(view)
        {
            serverField = view.FindViewById<EditText> (Resource.Id.exchange_server);
            domainField = view.FindViewById<EditText> (Resource.Id.exchange_domain);
            usernameField = view.FindViewById<EditText> (Resource.Id.exchange_username);
            serverField.TextChanged += TextFieldChanged;
            domainField.TextChanged += TextFieldChanged;
            usernameField.TextChanged += TextFieldChanged;
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
                serverField.Alpha = 0.6f;
            }
            if (!String.IsNullOrEmpty (config.Username)) {
                LockUsernameField = true;
                usernameField.Enabled = false;
                usernameField.Alpha = 0.6f;
            }
            if (!String.IsNullOrEmpty (config.Domain)) {
                LockDomainField = true;
                domainField.Enabled = false;
                domainField.Alpha = 0.6f;
            }
        }

        void TextFieldChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            AccountDelegate.AdvancedFieldsControllerDidChange (this);
        }
    }
}

