//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public class NcCommStatusEventArgs : EventArgs
    {
        public NcCommStatus.CommStatusEnum Status;
        public NcCommStatus.CommSpeedEnum Speed;
        public bool UserInterventionIsRequired;

        public NcCommStatusEventArgs (
            NcCommStatus.CommStatusEnum status, 
            NcCommStatus.CommSpeedEnum speed,
            bool userInterventionIsRequired)
        {
            Status = status;
            Speed = speed;
            UserInterventionIsRequired = userInterventionIsRequired;
        }
    }
}

