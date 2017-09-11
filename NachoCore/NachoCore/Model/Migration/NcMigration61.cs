//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;
using System.IO;
using SQLite;

namespace NachoCore.Model
{
    public class NcMigration61 : NcMigration
    {

        IntPtr InsertStatement;
        IntPtr ConnectionHandle;
        static IntPtr NegativePointer = new IntPtr (-1);

        public NcMigration61 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            // Replaced by migration 62, after this migration had already run in development,
            // but before it had run in production.

            //NcModel.Instance.Db.Execute ("UPDATE McEmailMessage SET IsIndexed = 0");
            //NcModel.Instance.Db.Execute ("UPDATE McContact SET IndexVersion = 0");
            //var accounts = McAccount.GetAllAccounts ();
            //foreach (var account in accounts) {
            //    var indexPath = NcModel.Instance.GetIndexPath (account.Id);
            //    if (Directory.Exists (indexPath)) {
            //        Directory.Delete (indexPath, recursive: true);
            //    }
            //}
        }
    }
}

