//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class NcMigration11 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var account in Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange)) {
                token.ThrowIfCancellationRequested ();
                account.DaysToSyncEmail = NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.OneMonth_5;
                account.Update ();
                var protocolState = McProtocolState.QueryByAccountId<McProtocolState> (account.Id).FirstOrDefault ();
                if (null != protocolState) {
                    // to avoid a flood of soft deletes.
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.StrategyRung = 5;
                        return true;
                    });
                }
                UpdateProgress (1);
            }
        }
    }
}
