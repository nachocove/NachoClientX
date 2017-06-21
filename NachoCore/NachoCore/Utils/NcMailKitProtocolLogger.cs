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
        string LogPrefix;
        //ulong LogModule;
        public NcDebugProtocolLogger (Log log)
        {
            //LogModule = logModule;
            LogPrefix = log.Subsystem;
        }

        #region IProtocolLogger implementation
        public void LogConnect (System.Uri uri)
        {
            Console.WriteLine ("{0}: Connect {1}", LogPrefix, uri);
        }
        public void LogClient (byte[] buffer, int offset, int count)
        {
            logBuffer (true, buffer, offset, count);
        }
        public void LogServer (byte[] buffer, int offset, int count)
        {
            logBuffer (false, buffer, offset, count);
        }
        #endregion
        #region IDisposable implementation
        public void Dispose ()
        {
        }
        #endregion

        private void logBuffer (bool isRequest, byte[] buffer, int offset, int count)
        {
            byte[] logData = buffer.Skip (offset).Take (count).ToArray ();
            Console.WriteLine ("{0}: {1}: {2}",
                LogPrefix,
                isRequest ? "C" : "S",
                Encoding.UTF8.GetString (logData));
        }
    }
}

