//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoPlatform
{
    public sealed class Contacts : IPlatformContacts
    {
        public IEnumerable<McContact> GetContacts ()
        {
            return null;
        }
    }
}

