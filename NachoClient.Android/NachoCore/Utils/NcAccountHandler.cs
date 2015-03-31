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

namespace NachoCore.Model
{
    public class NcAccountHandler
    {
        private static volatile NcAccountHandler instance;
        private static object syncRoot = new Object ();
        public static string[] exemptTables = new string[]  { 
            "McAccount", "sqlite_sequence", "McMigration",
        };

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

        public void CreateAccount (McAccount.AccountServiceEnum service, string emailAddress, string password)
        {
            NcModel.Instance.RunInTransaction (() => {
                // Need to regex-validate UI inputs.
                // You will always need to supply user credentials (until certs, for sure).
                // You will always need to supply the user's email address.
                var account = new McAccount () { EmailAddr = emailAddress };
                account.Signature = "Sent from Nacho Mail";
                account.AccountService = service;
                account.DisplayName = McAccount.AccountServiceName (service);
                account.Insert ();
                var cred = new McCred () { 
                    AccountId = account.Id,
                    Username = emailAddress,
                };
                cred.Insert ();
                if (null != password) {
                    cred.UpdatePassword (password);
                }
                Log.Info (Log.LOG_UI, "CreateAccount: {0}/{1}/{2}", account.Id, cred.Id, service);
                NcApplication.Instance.Account = account;
                Telemetry.RecordAccountEmailAddress (NcApplication.Instance.Account);
                LoginHelpers.SetHasProvidedCreds (NcApplication.Instance.Account.Id, true);
            });
        }

        // delete the file 
        public bool DeleteRemovingAccountFile ()
        {
            var RemovingAccountLockFile = NcModel.Instance.GetRemovingAccountLockFilePath ();
            try
            {
                File.Delete(RemovingAccountLockFile);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        // remove all the db data referenced by the account related to the given id
        public void RemoveAccountDBData(int AccountId)
        {
            Log.Info (Log.LOG_DB, "RemoveAccount: removing db data for account {0}", AccountId);
            List<string> DeleteStatements = new List<string> ();

            List<McSQLiteMaster> AllTables = McSQLiteMaster.QueryAllTables ();
            foreach (McSQLiteMaster Table in AllTables) {
                if (!((IList<string>) exemptTables).Contains (Table.name)) {
                    Log.Info (Log.LOG_DB, "RemoveAccount: Will be removing rows from Table {0} for account {1}", Table.name, AccountId);
                    Regex r = new Regex("^[a-zA-Z0-9]*$");
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
                if (account != null){
                    account.Delete ();
                }
            });

            Log.Info (Log.LOG_DB, "RemoveAccount: removed db data for account {0}", AccountId);

            //Log.Info (Log.LOG_UI, "RemoveAccount: McAccount column is {0}", CI.Name);
            //SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY 1
        }

        // remove all the files referenced by the account related to the given id
        public void RemoveAccountFiles(int AccountId)
        {
            Log.Info (Log.LOG_DB, "RemoveAccount: removing file data for account {0}", AccountId);
            String AccountDirPath = NcModel.Instance.GetAccountDirPath (AccountId);
            try{
                Directory.Delete(AccountDirPath, true);
            }
            catch (Exception e)
            {
                Log.Error (Log.LOG_DB, "RemoveAccount: cannot remove file data for account {0}. {1}", AccountId, e.Message);
            }
        }

        // remove all the db data and files referenced by the account related to the given id
        public void RemoveAccountDBAndFilesForAccountId(int AccountId)
        {
            // remove password from keychain
            var cred = McCred.QueryByAccountId<McCred> (AccountId).SingleOrDefault ();
            if (Keychain.Instance.HasKeychain ()) {
                Keychain.Instance.DeletePassword (cred.Id);
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
        public void RemoveAccount (bool stopStartServices = true)
        {
            if (null != NcApplication.Instance.Account) {
                // mark account is being removed so that we don't do anything else other than the remove till it is completed.
                NcModel.Instance.WriteRemovingAccountIdToFile (NcApplication.Instance.Account.Id);

                Log.Info (Log.LOG_UI, "RemoveAccount: user removed account {0}", NcApplication.Instance.Account.Id);
                BackEnd.Instance.Stop (NcApplication.Instance.Account.Id);

                if (stopStartServices) {
                    NcApplication.Instance.StopClass4Services ();
                    Log.Info (Log.LOG_UI, "RemoveAccount: StopClass4Services complete");
                    NcApplication.Instance.StopBasalServices ();
                    Log.Info (Log.LOG_UI, "RemoveAccount: StopBasalServices complete");
                }

                RemoveAccountDBAndFilesForAccountId (NcApplication.Instance.Account.Id);

                if (stopStartServices) {
                    NcApplication.Instance.StartBasalServices ();
                    Log.Info (Log.LOG_UI, "RemoveAccount:  StartBasalServices complete");
                    NcApplication.Instance.StartClass4Services ();
                    Log.Info (Log.LOG_UI, "RemoveAccount: StartClass4Services complete");
                }
            }
        }
    }
}

