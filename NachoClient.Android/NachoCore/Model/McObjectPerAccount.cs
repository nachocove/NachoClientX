//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;

namespace NachoCore.Model
{
    public class McObjectPerAccount : McObject
    {
        [Indexed]
        public int AccountId { get; set; }

        public McObjectPerAccount ()
        {
        }
    }
}

