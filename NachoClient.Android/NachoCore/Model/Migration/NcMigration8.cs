//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration8 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McFolder> ().Where (x => false == x.IsClientOwned). Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var folder in Db.Table<McFolder> ().Where (x => false == x.IsClientOwned)) {
                token.ThrowIfCancellationRequested ();
                var path = McPath.QueryByServerId (folder.AccountId, folder.ServerId);
                if (null != path) {
                    path.IsFolder = true;
                    path.Update ();
                }
            }
        }
    }
}

