//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McAccountHandler
    {
        private static volatile McAccountHandler instance;
        private static object syncRoot = new Object ();

        public static McAccountHandler Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new McAccountHandler ();
                        }
                    }
                }
                return instance; 
            }
        }
        public McAccountHandler ()
        {
        }

        // TODO this needs to get moved out of AppDelegate.
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
                // TODO: move LoginHelpers to appropriate location so that we don't need to reference NachoClient.iOS namespace here. I think, LoginHelpers should sit in Model.
                NachoClient.iOS.LoginHelpers.SetHasProvidedCreds (NcApplication.Instance.Account.Id, true);
            });
        }

        // TODO : keeping these here for now, till I confirm that we want to user userdefaults for saving this value

        // Get the AccountId for the account being removed
        public int GetRemovingAccountIdFromFile ()
        {
            string AccountIdString;
            int AccountId = 0;
            var RemovingAccountLockFile = NcModel.Instance.GetRemovingAccounLockFilePath ();
            if (File.Exists (RemovingAccountLockFile)) {
                // Get the account id from the file
                using (var stream = new FileStream (RemovingAccountLockFile, FileMode.Open, FileAccess.Read)) {
                    using (var reader = new StreamReader (stream)) {
                        AccountIdString = reader.ReadLine ();
                        int.TryParse(AccountIdString, out AccountId);
                    }
                }
            }
            return AccountId;
        }

        // write the removing AccountId to file
        public void WriteRemovingAccountIdToFile (int AccountId)
        {
            var RemovingAccountLockFile = NcModel.Instance.GetRemovingAccounLockFilePath ();
            using (var stream = new FileStream (RemovingAccountLockFile, FileMode.Create, FileAccess.Write)) {
                using (var writer = new StreamWriter (stream)) {
                    writer.WriteLine (AccountId);
                }
            }
        }

        // delete the file 
        public bool DeleteRemovingAccounFile ()
        {
            var RemovingAccountLockFile = NcModel.Instance.GetRemovingAccounLockFilePath ();
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
        public void RemoveAccountDBData(int Id)
        {
            Log.Info (Log.LOG_UI, "RemoveAccount: removing db data for account {0}", Id);
            List<string> DeleteStatements = new List<string> ();

            List<McSQLiteMaster> AllTables = McSQLiteMaster.QueryAllTables ();
            foreach (McSQLiteMaster Table in AllTables) {
                List<SQLite.SQLiteConnection.ColumnInfo> Columns = NcModel.Instance.Db.GetTableInfo (Table.name);
                foreach (SQLite.SQLiteConnection.ColumnInfo Column in Columns) {
                    if (Column.Name == "AccountId") {
                        Log.Info (Log.LOG_UI, "RemoveAccount: Will be removing rows from Table {0} for account {1}", Table.name, Id);
                        DeleteStatements.Add ("DELETE FROM " + Table.name + " WHERE AccountId = ?");
                        break;
                    }
                }
            }
            Log.Info (Log.LOG_UI, "RemoveAccount: removing all table rows for account {0}", Id);
            NcModel.Instance.RunInTransaction (() => {
                foreach (string stmt in DeleteStatements) {
                    NcModel.Instance.Db.Execute (stmt, Id);
                }
                Log.Info (Log.LOG_UI, "RemoveAccount: removed McAccount for id {0}", Id);
                var account = McAccount.QueryById<McAccount> (Id);
                if (account != null){
                    account.Delete ();
                }
            });

            Log.Info (Log.LOG_UI, "RemoveAccount: removed db data for account {0}", Id);

            //Log.Info (Log.LOG_UI, "RemoveAccount: McAccount column is {0}", CI.Name);
            //SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY 1
        }

        // remove all the files referenced by the account related to the given id
        public void RemoveAccountFiles(int Id)
        {
            Log.Info (Log.LOG_UI, "RemoveAccount: removing file data for account {0}", Id);
            String AccountDirPath = NcModel.Instance.GetAccountDirPath (Id);
            Directory.Delete(AccountDirPath, true);
        }

        // remove all the db data and files referenced by the account related to the given id
        public void RemoveAccountDBAndFilesForId(int Id)
        {
            // delete all DB data for account id - is db running?
            RemoveAccountDBData (Id);

            // delete all file system data for account id
            RemoveAccountFiles (Id);

            //BackEnd.Instance.Remove (NcApplication.Instance.Account.Id);
            // if there is only one account. TODO: deal with multi-account
            NcApplication.Instance.Account = null;
            // if successful, unmark account is being removed since it is completed.
            DeleteRemovingAccounFile ();
        }

        // TODO this needs to get moved out of AppDelegate.
        // TODO - this need to handle multiple accounts
        public void RemoveAccount ()
        {
            if (null != NcApplication.Instance.Account) {
                // mark account is being removed so that we don't do anything else other than the remove till it is completed.
                WriteRemovingAccountIdToFile (NcApplication.Instance.Account.Id);

                Log.Info (Log.LOG_UI, "RemoveAccount: user removed account {0}", NcApplication.Instance.Account.Id);
                BackEnd.Instance.Stop (NcApplication.Instance.Account.Id);

                NcApplication.Instance.StopClass4Services();
                Log.Info (Log.LOG_UI, "RemoveAccount: StopClass4Services complete");
                NcApplication.Instance.StopBasalServices ();
                Log.Info (Log.LOG_UI, "RemoveAccount: StopBasalServices complete");

                RemoveAccountDBAndFilesForId (NcApplication.Instance.Account.Id);

                NcApplication.Instance.StartBasalServices ();
                Log.Info (Log.LOG_UI, "RemoveAccount:  StartBasalServices complete");
                NcApplication.Instance.StartClass4Services ();
                Log.Info (Log.LOG_UI, "RemoveAccount: StartClass4Services complete");
            }
        }
    }
}

