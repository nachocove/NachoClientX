//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using MailKit;
using NachoCore.Utils;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

namespace NachoCore.Utils
{
    public interface INcProtocolLogger {
        bool ShouldLog();
    }

    public class MailKitProtocolLogger : IProtocolLogger
    {
        private string logPrefix { get; set; }
        private ulong logModule { get; set; }
        private INcProtocolLogger ProtoContext { get; set; }

        string authPattern = "^.*(AUTH|AUTHENTICATE) (PLAIN) (.*)$";
        Regex AuthRegex;

        public MailKitProtocolLogger (string prefix, ulong module, INcProtocolLogger protoContext)
        {
            logPrefix = prefix;
            logModule = module;
            ProtoContext = protoContext;

            AuthRegex = new Regex(authPattern);
            NcAssert.NotNull (AuthRegex);
        }
        public void LogConnect (Uri uri)
        {
            if (true == ProtoContext.ShouldLog ()) {
                if (uri == null)
                    throw new ArgumentNullException ("uri");

                Log.Info (logModule, "Connected to {0}", uri);
            }
        }

        private string RedactString(string line)
        {
            if (AuthRegex.IsMatch (line)) {
                line = AuthRegex.Replace(line, "$1 $2 <elided>");
            }
            return line;
        }
        private void logBuffer (string prefix, byte[] buffer, int offset, int count)
        {
            if (true == ProtoContext.ShouldLog ()) {
                char[] delimiterChars = { '\n' };
                var lines = Encoding.UTF8.GetString (buffer.Skip (offset).Take (count).ToArray ()).Split (delimiterChars);

                Array.ForEach (lines, (line) => {
                    if (line.Length > 0) {
                        Log.Info (logModule, "{0} {1}{2}", logPrefix, prefix, RedactString (line));
                    }
                });
            }
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

