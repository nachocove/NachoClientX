//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public class NcMigration18 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return Db.Table<McBrainEvent> ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            foreach (var dbEvent in Db.Table<McBrainEvent>()) {
                var brainEvent = dbEvent.BrainEvent () as NcBrainMessageEvent;
                if (null == brainEvent) {
                    dbEvent.AccountId = NcBrainEvent.KNotSpecificAccountId;
                } else {
                    dbEvent.AccountId = (int)brainEvent.AccountId;
                }
                dbEvent.Update ();
                UpdateProgress (1);
            }
        }
    }
}

