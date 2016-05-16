//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration9 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McContact> ().Where (x => McAbstrItem.ItemSource.Unknown == x.Source).Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var contact in Db.Table<McContact> ().Where (x => McAbstrItem.ItemSource.Unknown == x.Source)) {
                token.ThrowIfCancellationRequested ();
                contact.Source = McAbstrItem.ItemSource.ActiveSync;
                contact.Update ();
                UpdateProgress (1);
            }
        }
    }
}
