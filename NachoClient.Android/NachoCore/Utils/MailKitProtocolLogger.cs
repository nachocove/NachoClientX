//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using MailKit;
using NachoCore.Utils;
using System.Text;
using System.Linq;

namespace NachoCore.Utils
{
    public class MailKitProtocolLogger : IProtocolLogger
    {
        private string logPrefix { get; set; }
        private ulong logModule { get; set; }

        public MailKitProtocolLogger (string prefix, ulong module)
        {
            logPrefix = logPrefix;
            logModule = module;
        }
        public void LogConnect (Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException ("uri");

            Log.Info (Log.LOG_SMTP, "Connected to {0}", uri);
        }

        private void logBuffer (string prefix, byte[] buffer, int offset, int count)
        {
            char[] delimiterChars = { '\n' };
            var lines = Encoding.UTF8.GetString (buffer.Skip (offset).Take (count).ToArray ()).Split (delimiterChars);

            Array.ForEach (lines, (line) => {
                if (line.Length > 0) {
                    Log.Info (logModule, "{0}{1}{2}", logPrefix, prefix, line);
                }
            });
        }

        public void LogClient (byte[] buffer, int offset, int count)
        {
            logBuffer ("C: ", buffer, offset, count);
        }

        public void LogServer (byte[] buffer, int offset, int count)
        {
            logBuffer ("S: ", buffer, offset, count);
        }

        public void Dispose ()
        {
        }
    }
}

