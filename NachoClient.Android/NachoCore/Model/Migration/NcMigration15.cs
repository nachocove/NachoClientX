﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
            foreach (var emailMessage in Db.Table<McEmailMessage> ().Where (x => false == x.HasBeenNotified)) {
                token.ThrowIfCancellationRequested ();
                emailMessage.HasBeenNotified = true;
                emailMessage.Update ();
                UpdateProgress (1);
            }
        }
    }
}

