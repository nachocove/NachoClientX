//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using SQLite;
using NachoCore.Index;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public class NcMigration34 : NcMigration
    {

        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            // Superceded by NcMigration61
            //NcIndex index;
            //foreach (var account in McAccount.GetAllAccounts ()) {
            //    index = Indexer.Instance.IndexForAccount (account.Id);
            //    using (var transaction = index.Transaction ()) {
            //        transaction.RemoveAllMessages ();
            //        transaction.Commit ();
            //    }
            //}
            //Db.Execute ("UPDATE McEmailMessage SET IsIndexed = 0");
        }
    }
}

