//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore
{
    public class NcStrategy
    {
        protected IBEContext BEContext;

        public NcStrategy (IBEContext beContext)
        {
            BEContext = beContext;
        }

        public bool PowerPermitsSpeculation ()
        {
            return (Power.Instance.PowerState != PowerStateEnum.Unknown && Power.Instance.BatteryLevel > 0.7) ||
                (Power.Instance.PowerStateIsPlugged () && Power.Instance.BatteryLevel > 0.2);
        }

        public virtual bool ANarrowFolderHasToClientExpected (int accountId)
        {
            NcAssert.True (false);
            return true;
        }
    }
}
