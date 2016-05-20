//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.Utils
{
    public class TelemetryBE_NOOP : ITelemetryBE
    {
        #region ITelemetryBE implementation

        public string GetUserName ()
        {
            return Device.Instance.Identity ();
        }

        public bool UploadEvents (string jsonFilePath)
        {
            return true;
        }

        #endregion
    }

    public class TelemetryJsonFileTable_NOOP : ITelementryDB
    {
        #region ITelementryDB implementation

        public string GetNextReadFile ()
        {
            return null;
        }

        public bool Add (TelemetryJsonEvent jsonEvent)
        {
            return true;
        }

        public void Remove (string fileName, out Action supportCallback)
        {
            supportCallback = null;
            return;
        }

        public void FinalizeAll ()
        {
        }

        #endregion
    }
}

