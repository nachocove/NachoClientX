//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    public class NcMigration15 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McEmailMessage> ().Where (x => false == x.HasBeenNotified).Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            NcModel.Instance.Db.Execute (
                "UPDATE McEmailMessage SET HasBeenNotified = ? WHERE HasBeenNotified = ?", true, false);
            UpdateProgress (TotalObjects);
        }
    }
}

