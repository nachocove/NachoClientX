//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using System.Text.RegularExpressions;
using NachoPlatform;
using System.Linq;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public class NcAccountHandler
    {
        private static volatile NcAccountHandler instance;
        private static object syncRoot = new Object ();

        public static NcAccountHandler Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new NcAccountHandler ();
                        }
                    }
                }
                return instance; 
            }
        }

        public NcAccountHandler ()
        {
        }

        public McAccount CreateAccount (McAccount.AccountServiceEnum service, string emailAddress,
                                        string accessToken, string refreshToken, uint expireSecs)
        {
            return CreateAccountCore (service, emailAddress, (accountId) => {
                var cred = new McCred () { 
                    AccountId = accountId,
                    CredType = McCred.CredTypeEnum.OAuth2,
                    Username = emailAddress,
                };
                cred.Insert ();
                cred.UpdateOauth2 (accessToken, refreshToken, expireSecs);
                return cred;
            }, null);
        }

        public McAccount CreateAccount (McAccount.AccountServiceEnum service, string emailAddress, string password)
        {
            return CreateAccountCore (service, emailAddress, (accountId) => {
                var cred = new McCred () { 
                    AccountId = accountId,
                    CredType = McCred.CredTypeEnum.Password,
                    Username = emailAddress,
                };
                cred.Insert ();
                if (null != password) {
                    // FIXME STEVE - why would password be null?
                    cred.UpdatePassword (password);
                }
                return cred;
            }, null);
        }

        public McAccount CreateAccount (NcMdmConfig config)
        {
            return CreateAccountCore (McAccount.AccountServiceEnum.Exchange, config.EmailAddr, (accountId) => {
                string username = config.Username;
                if (String.IsNullOrEmpty (username)) {
                    if (String.IsNullOrEmpty (config.Domain)) {
                        username = config.EmailAddr;
                    } else {
                        username = McCred.Join (config.Domain, "");
                    }
                } else {
                    username = McCred.Join (config.Domain, username);
                }
                var cred = new McCred () { 
                    AccountId = accountId,
                    CredType = McCred.CredTypeEnum.Password,
                    Username = username,
                };
                cred.Insert ();
                return cred;
            }, (account) => {
                account.IsMdmBased = true;
                if (!String.IsNullOrEmpty (config.BrandingName)) {
                    account.DisplayName = config.BrandingName;
                }
                if (!String.IsNullOrEmpty (config.Host)) {
                    var server = new McServer () { 
                        AccountId = account.Id,
                        Capabilities = McAccount.ActiveSyncCapabilities,
                    };
                    server.Host = config.Host;
                    if (config.Port.HasValue) {
                        server.Port = (int)config.Port.Value;
                    }
                    server.UsedBefore = false;
                    server.Insert ();
                }
            });
        }

        private McAccount CreateAccountCore (McAccount.AccountServiceEnum service, string emailAddress, Func<int, McCred> makeCred, Action<McAccount> customize)
        {
            var account = new McAccount () { EmailAddr = emailAddress };

            NcModel.Instance.RunInTransaction (() => {
                // Need to regex-validate UI inputs.
                // You will always need to supply user credentials (until certs, for sure).
                // You will always need to supply the user's email address.
                account.ConfigurationInProgress = McAccount.ConfigurationInProgressEnum.InProgress;
                account.Signature = "Sent from Nacho Mail";
                account.SetAccountService (service);
                account.DisplayName = NcServiceHelper.AccountServiceName (service);
                account.Insert ();
                if (customize != null) {
                    customize (account);
                    account.Update ();
                }
                var cred = makeCred (account.Id);
                Log.Info (Log.LOG_UI, "CreateAccount: {0}/{1}/{2}", account.Id, cred.Id, service);
                Telemetry.RecordAccountEmailAddress (account);
            });
            return account;
        }

        public bool MaybeCreateServersForIMAP (McAccount account, McAccount.AccountServiceEnum service)
        {
            int imapServerPort;
            int smtpServerPort;
            string imapServerName;
            string smtpServerName;

            switch (service) {
            case McAccount.AccountServiceEnum.Exchange:
                return false;
            case McAccount.AccountServiceEnum.GoogleExchange:
                return false;
            case McAccount.AccountServiceEnum.HotmailExchange:
                return false;
            case McAccount.AccountServiceEnum.IMAP_SMTP:
                return false;
            case McAccount.AccountServiceEnum.OutlookExchange:
                return false;
            case McAccount.AccountServiceEnum.Office365Exchange:
                return false;
            case McAccount.AccountServiceEnum.GoogleDefault:
                imapServerPort = 993;
                imapServerName = McServer.IMAP_GMail_Host;
                smtpServerPort = 587;
                smtpServerName = McServer.SMTP_GMail_Host;
                break;
            case McAccount.AccountServiceEnum.HotmailDefault:
                imapServerPort = 993;
                imapServerName = McServer.IMAP_Hotmail_Host;
                smtpServerPort = 587;
                smtpServerName = McServer.SMTP_Hotmail_Host;
                break;
            case McAccount.AccountServiceEnum.Aol:
                imapServerPort = 993;
                imapServerName = McServer.IMAP_Aol_Host;
                smtpServerPort = 587;
                smtpServerName = McServer.SMTP_Aol_Host;
                break;
            case McAccount.AccountServiceEnum.Yahoo:
                imapServerPort = 993;
                imapServerName = McServer.IMAP_Yahoo_Host;
                smtpServerPort = 587;
                smtpServerName = McServer.SMTP_Yahoo_Host;
                break;
            case McAccount.AccountServiceEnum.iCloud:
                imapServerPort = 993;
                imapServerName = McServer.IMAP_iCloud_Host;
                smtpServerPort = 587;
                smtpServerName = McServer.SMTP_iCloud_Host;
                break;
            default:
                NcAssert.CaseError ();
                return false;
            }

            var imapServer = McServer.Create (account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter, imapServerName, imapServerPort);
            imapServer.IsHardWired = true;
            var smtpServer = McServer.Create (account.Id, McAccount.AccountCapabilityEnum.EmailSender, smtpServerName, smtpServerPort);
            smtpServer.IsHardWired = true;
            NcModel.Instance.RunInTransaction (() => {
                imapServer.Insert ();
                smtpServer.Insert ();
            });
            Log.Info (Log.LOG_UI, "CreateServersForIMAP: {0}/{1}:{2}/{3}:{4}", account.Id, imapServerName, imapServerPort, smtpServerName, smtpServerPort);
            return true;
        }

        // delete the file
        public bool DeleteRemovingAccountFile ()
        {
            var RemovingAccountLockFile = NcModel.Instance.GetRemovingAccountLockFilePath ();
            try {
                File.Delete (RemovingAccountLockFile);
                return true;
            } catch (IOException) {
                return false;
            }
        }

        // remove all the db data referenced by the account related to the given id
        public void RemoveAccountDBData (int AccountId)
        {
            Log.Info (Log.LOG_DB, "RemoveAccount: removing db data for account {0}", AccountId);
            List<string> DeleteStatements = new List<string> ();

            List<McSQLiteMaster> AllTables = McSQLiteMaster.QueryAllTables ();
            foreach (McSQLiteMaster Table in AllTables) {
                if (!((IList<string>)NcModel.ExemptTables).Contains (Table.name)) {
                    Log.Info (Log.LOG_DB, "RemoveAccount: Will be removing rows from Table {0} for account {1}", Table.name, AccountId);
                    Regex r = new Regex ("^[a-zA-Z0-9]*$");
                    if (r.IsMatch (Table.name)) {
                        DeleteStatements.Add ("DELETE FROM " + Table.name + " WHERE AccountId = ?");
                    } else {
                        Log.Warn (Log.LOG_DB, "RemoveAccount: Table name '{0}' is not alphanumeric. Possible SQL Injection. Rejecting....", Table.name);
                    }
                }
            }
            Log.Info (Log.LOG_DB, "RemoveAccount: removing all table rows for account {0}", AccountId);
            NcModel.Instance.RunInTransaction (() => {
                foreach (string stmt in DeleteStatements) {
                    NcModel.Instance.Db.Execute (stmt, AccountId);
                }
                Log.Info (Log.LOG_DB, "RemoveAccount: removed McAccount for id {0}", AccountId);
                var account = McAccount.QueryById<McAccount> (AccountId);
                if (account != null) {
                    account.Delete ();
                }
            });

            Log.Info (Log.LOG_DB, "RemoveAccount: removed db data for account {0}", AccountId);

            //Log.Info (Log.LOG_UI, "RemoveAccount: McAccount column is {0}", CI.Name);
            //SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY 1
        }

        // remove all the files referenced by the account related to the given id
        public void RemoveAccountFiles (int AccountId)
        {
            Log.Info (Log.LOG_DB, "RemoveAccount: removing file data for account {0}", AccountId);
            String AccountDirPath = NcModel.Instance.GetAccountDirPath (AccountId);
            try {
                var indexDirPath = NcModel.Instance.GetIndexPath (AccountId);
                if (Directory.Exists (indexDirPath)) {
                    NcBrain.MarkForDeletion (AccountId);
                }
                Directory.Delete (AccountDirPath, true);
            } catch (Exception e) {
                Log.Error (Log.LOG_DB, "RemoveAccount: cannot remove file data for account {0}. {1}", AccountId, e.Message);
            }
        }

        // remove all the db data and files referenced by the account related to the given id
        public void RemoveAccountDBAndFilesForAccountId (int AccountId)
        {
            // remove password from keychain
            var cred = McCred.QueryByAccountId<McCred> (AccountId).SingleOrDefault ();
            // TODO - add a wipe API to keychain.
            if (null != cred) {
                Keychain.Instance.DeletePassword (cred.Id);
                Keychain.Instance.DeleteAccessToken (cred.Id);
                Keychain.Instance.DeleteRefreshToken (cred.Id);
            }

            // delete all DB data for account id - is db running?
            RemoveAccountDBData (AccountId);

            // delete all file system data for account id
            RemoveAccountFiles (AccountId);

            // if there is only one account. TODO: deal with multi-account
            NcApplication.Instance.Account = null;
            // if successful, unmark account is being removed since it is completed.
            DeleteRemovingAccountFile ();
        }

        // TODO - this need to handle multiple accounts
        public void RemoveAccount (int AccountId, bool stopStartServices = true)
        {
            // mark account is being removed so that we don't do anything else other than the remove till it is completed.
            NcModel.Instance.WriteRemovingAccountIdToFile (AccountId);

            Log.Info (Log.LOG_UI, "RemoveAccount: user removed account {0}", AccountId);
            BackEnd.Instance.Stop (AccountId);

            if (stopStartServices) {
                NcApplication.Instance.StopClass4Services ();
                Log.Info (Log.LOG_UI, "RemoveAccount: StopClass4Services complete");
                NcApplication.Instance.StopBasalServices ();
                Log.Info (Log.LOG_UI, "RemoveAccount: StopBasalServices complete");
            }

            BackEnd.Instance.RemoveServices (AccountId);
            RemoveAccountDBAndFilesForAccountId (AccountId);

            if (stopStartServices) {
                NcApplication.Instance.StartBasalServices ();
                Log.Info (Log.LOG_UI, "RemoveAccount:  StartBasalServices complete");
                NcApplication.Instance.StartClass4Services ();
                Log.Info (Log.LOG_UI, "RemoveAccount: StartClass4Services complete");
            }
        }
    }
}

