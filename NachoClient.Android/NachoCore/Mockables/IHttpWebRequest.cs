//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public interface IHttpWebRequest
    {
        Uri Address { get; }
        Uri RequestUri { get; }
    }
}

