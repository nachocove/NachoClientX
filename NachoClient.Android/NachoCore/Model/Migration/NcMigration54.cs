﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore;
using System.IO;

namespace NachoClient.AndroidClient
{
    public class NcMigration54 : NcMigration
    {
        public NcMigration54 ()
        {
        }

        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            NachoCore.Utils.EmailHelper.SetHowToDisplayUnreadCount (NachoCore.Utils.EmailHelper.ShowUnreadEnum.RecentMessages);
        }
    }
}
