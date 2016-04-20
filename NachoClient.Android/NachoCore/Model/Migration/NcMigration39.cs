//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.IMAP;
using NachoCore.SMTP;

namespace NachoCore.Model
{
    public class NcMigration39 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.IMAP_SMTP).Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var account in Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.IMAP_SMTP)) {
                foreach (var protocolState in McProtocolState.QueryByAccountId<McProtocolState> (account.Id)) {
                    uint imapAdjusted = 0;
                    uint smtpAdjusted = 0;
                    if (protocolState.ImapProtoControlState > (uint)ImapProtoControl.Lst.FSyncW) {
                        imapAdjusted = protocolState.ImapProtoControlState - 1;
                    }
                    if (protocolState.SmtpProtoControlState > (uint)SmtpProtoControl.Lst.ConnW) {
                        smtpAdjusted = protocolState.SmtpProtoControlState - 1;
                    }
                    if (imapAdjusted != 0 || smtpAdjusted != 0) {
                        protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                            var target = (McProtocolState)record;
                            if (imapAdjusted != 0) {
                                target.ImapProtoControlState = imapAdjusted;
                            }
                            if (smtpAdjusted != 0) {
                                target.SmtpProtoControlState = smtpAdjusted;
                            }
                            return true;
                        });
                    }
                }
            }
        }
    }
}

