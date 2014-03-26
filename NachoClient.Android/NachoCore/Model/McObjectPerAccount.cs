//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;

namespace NachoCore.Model
{
    // If SQLite.Net would tolerate an abstract class, we'd be one.
    public class McObjectPerAccount : McObject
    {
        [Indexed]
        public int AccountId { get; set; }

        public override int Insert ()
        {
            NachoAssert.True (0 < AccountId);
            return base.Insert ();
        }

        public override int Delete ()
        {
            NachoAssert.True (0 < AccountId);
            return base.Delete ();
        }

        public override int Update ()
        {
            NachoAssert.True (0 < AccountId);
            return base.Update ();
        }
    }
}

