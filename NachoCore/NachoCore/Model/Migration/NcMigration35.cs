//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.Model
{
    public class NcMigration35 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return 0;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
        }
    }
}
