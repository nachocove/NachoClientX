//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class NcMigration38 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var account in Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange)) {
                foreach (var protocolState in McProtocolState.QueryByAccountId<McProtocolState> (account.Id)) {
                    if (protocolState.ProtoControlState > (uint)AsProtoControl.Lst.FSync2W) {
                        uint adjusted = protocolState.ProtoControlState - 1;
                        protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                            var target = (McProtocolState)record;
                            target.ProtoControlState = adjusted;
                            return true;
                        });
                    }
                }
            }
        }
    }
}

