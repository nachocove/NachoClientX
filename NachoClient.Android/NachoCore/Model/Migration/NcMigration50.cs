//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore;
using System.IO;

namespace NachoClient.AndroidClient
{
    public class NcMigration50 : NcMigration
    {
        public NcMigration50 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            var dataRoot = NcApplication.GetDataDirPath ();
            var accounts = McAccount.GetAllAccounts ();
            foreach (var account in accounts) {
                var bundlesRoot = Path.Combine (dataRoot, "files", account.Id.ToString (), "bundles");
                if (Directory.Exists (bundlesRoot)) {
                    Directory.Delete (bundlesRoot, true);
                }
            }
        }
    }
}

