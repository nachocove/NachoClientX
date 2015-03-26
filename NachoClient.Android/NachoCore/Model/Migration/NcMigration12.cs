//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Threading;

namespace NachoCore.Model
{
    public class NcMigration12 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McEmailMessageScoreSyncInfo> ().Count ();
        }

        public override void Run (CancellationToken token)
        {
            McAccount ExchangeAccount =  McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Exchange).SingleOrDefault ();

            int rowsUpdated =  NcModel.Instance.Db.Execute ("DELETE FROM McEmailMessageScoreSyncInfo WHERE AccountId = ?", ExchangeAccount.Id);
            UpdateProgress (rowsUpdated);
        }
    }
}

