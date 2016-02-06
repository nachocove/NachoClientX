//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore;
using System.IO;

namespace NachoClient.AndroidClient
{
    public class NcMigration51 : NcMigration
    {
        public NcMigration51 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            string query =
                "INSERT INTO McEmailMessageNeedsUpdate (EmailMessageId, AccountId, NeedsUpdate) " +
                " SELECT Id, AccountId, NeedUpdate FROM McEmailMessage";
            NcModel.Instance.Db.Execute (query);
        }
    }
}

