//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using MailKit;
using NachoCore.Utils;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;

namespace NachoCore.Utils
{
    public class NcDebugProtocolLogger : IProtocolLogger
    {
        Log Log;

        public NcDebugProtocolLogger (Log log)
        {
            Log = log;
        }

        #region IProtocolLogger implementation
        public void LogConnect (System.Uri uri)
        {
            Log.Debug ("Connect {0}", uri);
        }
        public void LogClient (byte [] buffer, int offset, int count)
        {
            logBuffer (true, buffer, offset, count);
        }
        public void LogServer (byte [] buffer, int offset, int count)
        {
            logBuffer (false, buffer, offset, count);
        }
        #endregion
        #region IDisposable implementation
        public void Dispose ()
        {
        }
        #endregion

        private void logBuffer (bool isRequest, byte [] buffer, int offset, int count)
        {
            byte [] logData = buffer.Skip (offset).Take (count).ToArray ();
            Log.Debug ("{0}: {1}", isRequest ? "C" : "S", Encoding.UTF8.GetString (logData));
        }
    }
}

