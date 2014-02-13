//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public class NcCommStatusServerEventArgs : EventArgs
    { 
        public int ServerId;
        public NcCommStatus.CommQualityEnum Quality;
        public NcCommStatus.CommStatusEnum Status;
        public NcCommStatus.CommSpeedEnum Speed;

        public NcCommStatusServerEventArgs (int serverId, 
                                            NcCommStatus.CommQualityEnum quality, 
                                            NcCommStatus.CommStatusEnum status, 
                                            NcCommStatus.CommSpeedEnum speed)
        {
            ServerId = serverId;
            Quality = quality;
            Status = status;
            Speed = speed;
        }
    }
}

