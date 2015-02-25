//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoMessageViewer
    {
        void SetSingleMessageThread (McEmailMessageThread thread);
    }
}
