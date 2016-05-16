//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore;
using System.IO;

namespace NachoClient.AndroidClient
{
    public class NcMigration53 : NcMigration
    {
        public NcMigration53 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var account in McAccount.GetAllAccounts()) {
                account.AssignOpenColorIndex ();
                account.Update ();
            }
        }
    }
}

