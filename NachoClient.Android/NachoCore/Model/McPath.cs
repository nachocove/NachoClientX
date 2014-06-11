//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;

namespace NachoCore.Model
{
    public class McPath : McObjectPerAccount
    {
        [Indexed]
        public string ParentId { get; set; }

        [Indexed]
        public string ServerId { get; set; }
    }
}

