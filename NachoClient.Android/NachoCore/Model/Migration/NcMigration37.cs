//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.Model
{
    public class NcMigration37 : NcMigration
    {
        string querySQLTemplate = "SELECT {0} FROM McAttachment WHERE AccountId = ?";

        public override int GetNumberOfObjects ()
        {
            int count = 0;
            foreach (var account in NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.IMAP_SMTP)) {
                count += NcModel.Instance.Db.ExecuteScalar<int> (string.Format (querySQLTemplate, "COUNT( Distinct ItemId )"), account.Id);
            }
            return count;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var account in NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.IMAP_SMTP)) {
                foreach (var emailId in NcModel.Instance.Db.Query<NcEmailMessageIndex> (string.Format (querySQLTemplate, "Distinct ItemId as Id"), account.Id)) {
                    token.ThrowIfCancellationRequested ();
                    Db.Execute ("UPDATE McEmailMessage SET cachedHasAttachments = 1 WHERE Id = ?", emailId.Id);
                }
            }
        }
    }
}
