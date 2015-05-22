//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration26 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return NcModel.Instance.Db.Table<McAccount> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            var accounts = NcModel.Instance.Db.Table<McAccount> ();
            foreach (var account in accounts) {
                // Make sure AccountService is initialized
                if (McAccount.AccountServiceEnum.None == account.AccountService) {
                    if (McAccount.AccountTypeEnum.Exchange == account.AccountType) {
                        account.SetAccountService(McAccount.AccountServiceEnum.Exchange);
                    }
                    if (McAccount.AccountTypeEnum.IMAP_SMTP == account.AccountType) {
                        account.SetAccountService(McAccount.AccountServiceEnum.GoogleDefault);
                    }
                }
            }
            UpdateProgress (1);
        }
    }
}

