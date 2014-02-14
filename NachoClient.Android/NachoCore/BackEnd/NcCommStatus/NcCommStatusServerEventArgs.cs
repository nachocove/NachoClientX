//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public class NcCommStatusServerEventArgs : EventArgs
    {
        public int ServerId;
        public NcCommStatus.CommQualityEnum Quality;

        public NcCommStatusServerEventArgs (int serverId, NcCommStatus.CommQualityEnum quality)
        {
            ServerId = serverId;
            Quality = quality;
        }
    }
}

