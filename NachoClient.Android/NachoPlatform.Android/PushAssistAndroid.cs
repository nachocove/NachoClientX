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

