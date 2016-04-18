//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore.Model
{
    public class NcMigration57 : NcMigration
    {
        Dictionary<int, List<NcEmailMessageIndex>> messages;

        public override int GetNumberOfObjects ()
        {
            messages = new Dictionary<int, List<NcEmailMessageIndex>> ();
            var accounts = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.IMAP_SMTP).Where (x => x.AccountService == McAccount.AccountServiceEnum.GoogleDefault).ToList ();
            foreach (var account in accounts) {
                var emails = QueryEmailsByAccountId (account.Id);
                if (emails.Count > 0) {
                    messages [account.Id] = emails;
                }
            }
            return messages.Keys.Count;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            if (messages.Count > 0) {
                foreach (var accountId in messages.Keys) {
                    NcModel.Instance.Db.RunInTransaction (() => {
                        // delete all emails
                        foreach (var emailId in messages[accountId]) {
                            var email = emailId.GetMessage ();
                            if (email != null) {
                                email.Delete ();
                            }
                        }
                        // reset folder variables
                        foreach (var folder in McFolder.QueryByAccountId<McFolder> (accountId)) {
                            folder.ImapNeedFullSync = true;
                            folder.ImapUidNext = 0;
                            folder.ImapUidSet = "";
                            folder.Update ();
                        }
                        // set rung back to 0 
                        var protocolState = McProtocolState.QueryByAccountId<McProtocolState> (accountId).FirstOrDefault ();
                        if (protocolState != null) {
                            protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                                var target = (McProtocolState)record;
                                target.ImapSyncRung = 0;
                                return true;
                            });
                        }
                    });
                    UpdateProgress (1);
                }
            }
        }

        List<NcEmailMessageIndex> QueryEmailsByAccountId (int accountId)
        {
            return NcModel.Instance.Db.Query<NcEmailMessageIndex> ("SELECT Id from McEmailMessage where AccountId=?", accountId);
        }
    }
}

