//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Linq;

namespace NachoClient.AndroidClient
{
    public class NcMigration35 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            int count = 0;
            foreach (var account in NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.IMAP_SMTP)) {
                count += NcModel.Instance.Db.Table<McEmailMessage> ().Where (e => e.AccountId == account.Id && e.ImapUid == 0).Count ();
            }
            return count;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var account in NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.IMAP_SMTP)) {
                var emails = NcModel.Instance.Db.Table<McEmailMessage> ().Where (e => e.AccountId == account.Id && e.ImapUid == 0);
                foreach (McEmailMessage email in emails) {
                    token.ThrowIfCancellationRequested ();
                    Db.Execute ("UPDATE McEmailMessage SET ImapUid = ? WHERE Id = ?",
                        ImapMessageUid (email.ServerId), email.Id);
                }
            }
        }

        private static uint ImapMessageUid (string MessageServerId)
        {
            return UInt32.Parse (MessageServerId.Split (':') [1]);
        }
    }
}
