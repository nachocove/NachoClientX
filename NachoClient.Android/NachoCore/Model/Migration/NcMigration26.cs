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
                    bool update = false;
                    if (McAccount.AccountTypeEnum.Exchange == account.AccountType) {
                        account.AccountService = McAccount.AccountServiceEnum.Exchange;
                        update = true;
                    }
                    if (McAccount.AccountTypeEnum.IMAP_SMTP == account.AccountType) {
                        account.AccountService = McAccount.AccountServiceEnum.GoogleDefault;
                        update = true;
                    }
                    if (McAccount.AccountTypeEnum.Device == account.AccountType) {
                        if (McAccount.AccountServiceEnum.Device != account.AccountService) {
                            account.AccountService = McAccount.AccountServiceEnum.Device;
                            update = true;
                        }
                    }
                    if (update) {
                        account.Update ();
                    }
                }
            }
            UpdateProgress (1);
        }
    }
}

