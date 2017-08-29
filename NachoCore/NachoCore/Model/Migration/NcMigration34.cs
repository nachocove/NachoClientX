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
        protected TableQuery<McEmailMessage> IndexedEmailMessages ()
        {
            return Db.Table<McEmailMessage> ().Where ((e) => (e.IsIndexed > 0) && (e.IsIndexed < EmailMessageIndexDocument.Version));
        }

        public override int GetNumberOfObjects ()
        {
            return IndexedEmailMessages ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            NcIndex index;
            foreach (var account in McAccount.GetAllAccounts ()) {
                index = Indexer.Instance.IndexForAccount (account.Id);
                if (index.BeginRemoveTransaction ()) {
                    var numUpdated = index.BulkRemoveEmailMessage ();
                    index.EndRemoveTransaction ();
                    UpdateProgress (numUpdated);
                }
            }
            Db.Execute ("UPDATE McEmailMessage SET IsIndexed = 0");
        }
    }
}

