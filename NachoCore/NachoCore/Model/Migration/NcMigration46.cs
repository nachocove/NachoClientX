//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class NcMigration46 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return NcModel.Instance.Db.Table<McCred> ().Where (x => x.CredType == McCred.CredTypeEnum.OAuth2 && x.ExpirySecs == 0).Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            NcModel.Instance.Db.Execute ("UPDATE McCred SET ExpirySecs=3600 WHERE CredType = ? AND ExpirySecs = 0", McCred.CredTypeEnum.OAuth2);
        }
    }
}
