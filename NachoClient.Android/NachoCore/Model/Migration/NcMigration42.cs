//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    /// <summary>
    /// Migrate the McAccount.AccountCapability field.
    /// </summary>
    /// <remarks>
    /// This migration was created long after the AccountCapability field came into use.  This migration
    /// has to be careful to not overwrite the field in accounts that were created more recently.
    /// </remarks>
    public class NcMigration42 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange && x.AccountCapability == 0).Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var oldExchangeAccount in NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange && x.AccountCapability == 0)) {
                token.ThrowIfCancellationRequested ();
                oldExchangeAccount.AccountCapability = McAccount.ActiveSyncCapabilities;
                oldExchangeAccount.Update ();
                UpdateProgress (1);
            }
        }
    }
}

