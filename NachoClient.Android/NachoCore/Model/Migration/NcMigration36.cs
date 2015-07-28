//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
<<<<<<< HEAD
using System.Linq;
=======
>>>>>>> master

namespace NachoClient.AndroidClient
{
    public class NcMigration36 : NcMigration
    {
<<<<<<< HEAD
        string querySQLTemplate = "SELECT {0} FROM McAttachment WHERE AccountId = ?";
=======
        static string querySQLTemplate = "SELECT {0} FROM McEmailMessage WHERE AccountId = ? AND ImapUid = 0 AND ServerId <> '' AND ServerId IS NOT NULL";
>>>>>>> master

        public override int GetNumberOfObjects ()
        {
            int count = 0;
            foreach (var account in NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.IMAP_SMTP)) {
<<<<<<< HEAD
                count += NcModel.Instance.Db.ExecuteScalar<int> (string.Format (querySQLTemplate, "COUNT( Distinct ItemId )"), account.Id);
=======
                count += NcModel.Instance.Db.ExecuteScalar<int> (string.Format (querySQLTemplate, "COUNT(*)"), account.Id);
>>>>>>> master
            }
            return count;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var account in NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.IMAP_SMTP)) {
<<<<<<< HEAD
                foreach (var emailId in NcModel.Instance.Db.Query<NcEmailMessageIndex> (string.Format (querySQLTemplate, "Distinct ItemId as Id"), account.Id)) {
                    token.ThrowIfCancellationRequested ();
                    Db.Execute ("UPDATE McEmailMessage SET cachedHasAttachments = 1 WHERE Id = ?", emailId.Id);
                }
            }
        }
    }
}
=======
                var emails = NcModel.Instance.Db.Query<McEmailMessage> (string.Format (querySQLTemplate, "*"), account.Id);
                foreach (McEmailMessage email in emails) {
                    token.ThrowIfCancellationRequested ();
                    Db.Execute ("UPDATE McEmailMessage SET ImapUid = ? WHERE Id = ?", ImapMessageUid (email.ServerId), email.Id);
                }
            }
        }

        private static uint ImapMessageUid (string MessageServerId)
        {
            return UInt32.Parse (MessageServerId.Split (':') [1]);
        }
    }
}

>>>>>>> master
