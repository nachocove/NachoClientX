//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Threading;

namespace NachoCore.Model
{
    // This migration is no longer valid because the old sync info tables are deprecated.
    public class NcMigration12 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return 0;
            ////return Db.Table<McEmailMessageScoreSyncInfo> ().Count ();
        }

        public override void Run (CancellationToken token)
        {
            ////McAccount ExchangeAccount =  McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Exchange).SingleOrDefault ();
            ////
            ////if (ExchangeAccount != null) {
            ////    int rowsUpdated = NcModel.Instance.Db.Execute ("DELETE FROM McEmailMessageScoreSyncInfo WHERE AccountId = ?", ExchangeAccount.Id);
            ////    UpdateProgress (rowsUpdated);
            ////}
        }
    }
}

