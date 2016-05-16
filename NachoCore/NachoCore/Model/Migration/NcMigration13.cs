//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Threading;

namespace NachoCore.Model
{
    public class NcMigration13 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McNote> ().Count ();
        }

        public override void Run (CancellationToken token)
        {
            McAccount ExchangeAccount =  McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Exchange).SingleOrDefault ();

            foreach (var note in Db.Table<McNote>()) {
                token.ThrowIfCancellationRequested ();
                note.AccountId = ExchangeAccount.Id;
                note.Update ();
                UpdateProgress (1);
            }
        }
    }
}

