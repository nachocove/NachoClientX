//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public class StatusIndEventArgs : EventArgs
    {
        public McAccount Account;
        public NcResult Status;
        public string[] Tokens;
    }
}
