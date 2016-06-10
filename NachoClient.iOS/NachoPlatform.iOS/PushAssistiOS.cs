//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore;

namespace NachoPlatform
{
    public class PushAssist : IPushAssist
    {
        public PushAssist (IPushAssistOwner owner)
        {
        }

        public static bool SetDeviceToken (string token)
        {
            return true;
        }

        public static bool ProcessRemoteNotification (PingerNotification pinger, NotificationFetchFunc fetch)
        {
            return true;
        }

        #region IPushAssist implementation

        public void Dispose ()
        {
        }

        public void Execute ()
        {
        }

        public void Park ()
        {
        }

        public bool IsStartOrParked ()
        {
            return false;
        }

        public void Stop ()
        {
        }

        #endregion
    }
}

