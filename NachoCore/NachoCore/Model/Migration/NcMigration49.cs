//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.Model
{
    /// <summary>
    /// Clean up orphaned ancillary data for contacts.
    /// </summary>
    public class NcMigration49 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            // There are four tables that will be checked.  The number of rows that will be deleted
            // as part of the cleaning is not worth calculating in advance.
            return 4;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            int deletedRows = NcModel.Instance.Db.Execute (
                "DELETE FROM McContactStringAttribute WHERE ContactId NOT IN (SELECT Id FROM McContact)");
            if (0 < deletedRows) {
                Log.Warn (Log.LOG_DB,
                    "Deleted {0} orphaned McContactStringAttribute rows during migration.", deletedRows);
            }
            UpdateProgress (1);

            deletedRows = NcModel.Instance.Db.Execute (
                "DELETE FROM McContactDateAttribute WHERE ContactId NOT IN (SELECT Id FROM McContact)");
            if (0 < deletedRows) {
                Log.Warn (Log.LOG_DB,
                    "Deleted {0} orphaned McContactDateAttribute rows during migration.", deletedRows);
            }
            UpdateProgress (1);

            deletedRows = NcModel.Instance.Db.Execute (
                "DELETE FROM McContactAddressAttribute WHERE ContactId NOT IN (SELECT Id FROM McContact)");
            if (0 < deletedRows) {
                Log.Warn (Log.LOG_DB,
                    "Deleted {0} orphaned McContactAddressAttribute rows during migration.", deletedRows);
            }
            UpdateProgress (1);

            deletedRows = NcModel.Instance.Db.Execute (
                "DELETE FROM McContactEmailAddressAttribute WHERE ContactId NOT IN (SELECT Id FROM McContact)");
            if (0 < deletedRows) {
                Log.Warn (Log.LOG_DB,
                    "Deleted {0} orphaned McContactEmailAddressAttribute rows during migration.", deletedRows);
            }
            UpdateProgress (1);
        }
    }
}

