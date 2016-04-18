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
            int objectCount = 0;
            messages = new Dictionary<int, List<NcEmailMessageIndex>> ();
            var accounts = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.IMAP_SMTP).Where (x => x.AccountService == McAccount.AccountServiceEnum.GoogleDefault).ToList ();
            foreach (var account in accounts) {
                var emails = QueryEmailsByAccountId (account.Id);
                if (emails.Count > 0) {
                    messages [account.Id] = emails;
                    objectCount += emails.Count;
                } else {
                    messages [account.Id] = null;
                }
                objectCount += McFolder.QueryByAccountId<McFolder> (account.Id).Count ();
                if (null != McProtocolState.QueryByAccountId<McProtocolState> (account.Id).FirstOrDefault ()) {
                    objectCount++;
                }
            }
            return objectCount+(2*messages.Keys.Count);
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var accountId in messages.Keys) {
                // delete all emails
                // FIXME? This could be very slow with thousands of messages.
                if (null != messages [accountId]) {
                    foreach (var emailId in messages[accountId]) {
                        var email = emailId.GetMessage ();
                        if (email != null) {
                            email.Delete ();
                        }
                        UpdateProgress (1);
                    }
                }

                // reset folder variables
                foreach (var folder in McFolder.QueryByAccountId<McFolder> (accountId)) {
                    folder.UpdateWithOCApply<McFolder> ((record) => {
                        var target = (McFolder)record;
                        target.ImapNeedFullSync = true;
                        return true;
                    });
                    UpdateProgress (1);
                }

                // set rung back to 0 
                var protocolState = McProtocolState.QueryByAccountId<McProtocolState> (accountId).FirstOrDefault ();
                if (protocolState != null) {
                    protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.ImapSyncRung = 0;
                        return true;
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

