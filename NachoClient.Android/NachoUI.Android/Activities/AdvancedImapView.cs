
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

namespace NachoClient.AndroidClient
{
    public class AdvancedImapView : AccountAdvancedFieldsViewController
    {
        EditText usernameField;
        EditText incomingServerField;
        EditText incomingPortField;
        EditText outgoingServerField;
        EditText outgoingPortField;

        public AdvancedImapView(View view) : base(view)
        {
            usernameField = view.FindViewById<EditText>(Resource.Id.imap_username);
            incomingServerField = view.FindViewById<EditText>(Resource.Id.imap_server);
            incomingPortField = view.FindViewById<EditText>(Resource.Id.imap_port);
            outgoingServerField = view.FindViewById<EditText>(Resource.Id.smtp_server);
            outgoingPortField = view.FindViewById<EditText>(Resource.Id.smtp_port);
            usernameField.TextChanged += TextFieldChanged;
            incomingServerField.TextChanged += TextFieldChanged;
            incomingPortField.TextChanged += TextFieldChanged;
            outgoingServerField.TextChanged += TextFieldChanged;
            outgoingPortField.TextChanged += TextFieldChanged;
        }

        void TextFieldChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            AccountDelegate.AdvancedFieldsControllerDidChange (this);
        }

        public override bool CanSubmitFields ()
        {
            if (incomingServerField.Text.Trim().Length == 0) {
                return false;
            }
            if (incomingPortField.Text.Trim().Length == 0) {
                return false;
            }
            if (outgoingServerField.Text.Trim().Length == 0) {
                return false;
            }
            if (outgoingPortField.Text.Trim().Length == 0) {
                return false;
            }
            return true;
        }

        public override string IssueWithFields ()
        {
            if (!EmailHelper.IsValidHost (incomingServerField.Text)) {
                return "Invalid incoming server name. Please check that you typed it in correctly.";
            }
            if (!EmailHelper.IsValidHost (outgoingServerField.Text)) {
                return "Invalid outgoing server name. Please check that you typed it in correctly.";
            }
            if (incomingServerField.Text.Contains (":")) {
                return "Invalid incoming server name. Scheme or port number is not allowed.";
            }
            if (outgoingServerField.Text.Contains (":")) {
                return "Invalid outgoing server name. Scheme or port number is not allowed.";
            }
            string err = PortNumber_Helpers.CheckPortValidity (incomingPortField.Text, "incoming");
            if (!string.IsNullOrEmpty (err)) {
                return err;
            }
            err = PortNumber_Helpers.CheckPortValidity (outgoingPortField.Text, "outgoing");
            if (!string.IsNullOrEmpty (err)) {
                return err;
            }
            return null;
        }

        public override void PopulateAccountWithFields (NachoCore.Model.McAccount account)
        {
            var username = usernameField.Text;
            if (String.IsNullOrWhiteSpace (username)) {
                username = account.EmailAddr;
            }
            var cred = McCred.QueryByAccountId<McCred> (account.Id).Single ();
            cred.Username = username;
            cred.UserSpecifiedUsername = true;
            cred.Update ();

            var imapServerName = incomingServerField.Text;
            var smtpServerName = outgoingServerField.Text;

            int imapServerPort;
            var imapPortTryParse = int.TryParse (incomingPortField.Text, out imapServerPort);
            NcAssert.True (imapPortTryParse);

            int smtpServerPort;
            var smtpPortTryParse = int.TryParse (outgoingPortField.Text, out smtpServerPort);
            NcAssert.True (smtpPortTryParse);

            var imapServer = McServer.QueryByAccountIdAndCapabilities (account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter);
            if (imapServer == null) {
                imapServer = McServer.Create (account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter, imapServerName, imapServerPort);
                imapServer.Insert ();
                Log.Info (Log.LOG_UI, "ImapAdvancedFieldsViewController create IMAP server: {0}/{1}/{2}:{3}", account.Id, imapServer.Id, imapServerName, imapServerPort);
            } else {
                imapServer.Host = imapServerName;
                imapServer.Port = imapServerPort;
                imapServer.Update ();
                Log.Info (Log.LOG_UI, "ImapAdvancedFieldsViewController update IMAP server: {0}/{1}/{2}:{3}", account.Id, imapServer.Id, imapServerName, imapServerPort);
            }

            var smtpServer = McServer.QueryByAccountIdAndCapabilities (account.Id, McAccount.AccountCapabilityEnum.EmailSender);
            if (smtpServer == null) {
                smtpServer = McServer.Create (account.Id, McAccount.AccountCapabilityEnum.EmailSender, smtpServerName, smtpServerPort);
                smtpServer.Insert ();
                Log.Info (Log.LOG_UI, "ImapAdvancedFieldsViewController create SMTP server: {0}/{1}/{2}:{3}", account.Id, smtpServer.Id, smtpServerName, smtpServerPort);
            } else {
                smtpServer.Host = smtpServerName;
                smtpServer.Port = smtpServerPort;
                smtpServer.Update ();
                Log.Info (Log.LOG_UI, "ImapAdvancedFieldsViewController update SMTP server: {0}/{1}/{2}:{3}", account.Id, smtpServer.Id, smtpServerName, smtpServerPort);
            }
        }

        public override void PopulateFieldsWithAccount (NachoCore.Model.McAccount account)
        {
            if (account != null) {
                var creds = McCred.QueryByAccountId<McCred> (account.Id).Single ();

                if (creds != null) {
                    usernameField.Text = creds.Username;
                } else {
                    usernameField.Text = "";
                }

                var imapServer = McServer.QueryByAccountIdAndCapabilities (account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter);
                if (null != imapServer) {
                    incomingServerField.Text = imapServer.Host;
                    incomingPortField.Text = imapServer.Port.ToString ();
                } else {
                    incomingServerField.Text = "993";
                    incomingPortField.Text = "587";
                }
                var smtpServer = McServer.QueryByAccountIdAndCapabilities (account.Id, McAccount.AccountCapabilityEnum.EmailSender);
                if (null != smtpServer) {
                    outgoingServerField.Text = smtpServer.Host;
                    outgoingPortField.Text = smtpServer.Port.ToString ();
                }
            } else {
                usernameField.Text = "";
                incomingServerField.Text = "";
                incomingPortField.Text = "993";
                outgoingServerField.Text = "";
                outgoingPortField.Text = "587";
            }
        }

        public override void SetFieldsEnabled (bool enabled)
        {
            usernameField.Enabled = enabled;
            incomingServerField.Enabled = enabled;
            incomingPortField.Enabled = enabled;
            outgoingServerField.Enabled = enabled;
            outgoingPortField.Enabled = enabled;
        }

        public override void UnpopulateAccount (McAccount account)
        {
        } 
    }
}

