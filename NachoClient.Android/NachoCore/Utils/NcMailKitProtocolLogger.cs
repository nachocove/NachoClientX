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
    public delegate string RedactProtocolLogFuncDel (bool isRequest, string logData);
    public interface INcMailKitProtocolLogger {
        byte[] GetResponseBuffer ();
        byte[] GetRequestBuffer ();
        byte[] GetCombinedBuffer ();
        void ResetBuffers();
        bool Enabled ();
        void Start (RedactProtocolLogFuncDel func);
        void Stop ();
        void Stop (out byte[]RequestLog, out byte[] ResponseLog);
    }

    public class NcMailKitProtocolLogger : IProtocolLogger, INcMailKitProtocolLogger
    {
        public RedactProtocolLogFuncDel RedactProtocolLogFunc { get; private set; }
        public static RegexOptions rxOptions {
            get { return RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Multiline | RegexOptions.Singleline; }
        }
        public static readonly string ImapCommandNumRegexStr = @"(?<num>[A-Z]\d+ )";

        private byte[] logPrefix;
        private MemoryStream RequestLogBuffer;
        private MemoryStream ResponseLogBuffer;
        private MemoryStream CombinedLogBuffer;
        private bool _Enabled;

        private List<Regex> CommonRequestRegexList;
        private List<Regex> CommonResponseRegexList;
        private List<Regex> AuthRequestRegexList;

        private Regex ImapCommandRx;
        private bool pauseLogging;


        public NcMailKitProtocolLogger (string prefix)
        {
            logPrefix = Encoding.ASCII.GetBytes(prefix + " ");

            ResetBuffers ();
            _Enabled = false;
            RedactProtocolLogFunc = null;
            CommonRequestRegexList = new List<Regex> ();
            CommonResponseRegexList = new List<Regex> ();
            AuthRequestRegexList = new List<Regex> ();

            // All regex's must convert everything into one or more groups. A group-name that starts with 'redact', will be redacted.
            // Otherwise, the group will be copied as is.

            ImapCommandRx = new Regex (@"^" + ImapCommandNumRegexStr + @"(?<cmd>\w+)");

            //A00000013 EXAMINE "[Gmail]/Sent Mail" (CONDSTORE)
            //A00000013 SELECT "[Gmail]/Sent Mail" (CONDSTORE)
            CommonRequestRegexList.Add (new Regex (@"^" + ImapCommandNumRegexStr + @"(?<cmd>SELECT |EXAMINE )(?<redact>\""[^\""]+\"")(?<rest>.*)$", rxOptions));
            //A00000019 SELECT INBOX ....
            //A00000019 EXAMINE INBOX ....
            CommonRequestRegexList.Add (new Regex (@"^" + ImapCommandNumRegexStr + @"(?<cmd>SELECT |EXAMINE )(?<redact>\S+)(?<rest>.*)$", rxOptions));

            //A00000013 OK [READ-ONLY] [Gmail]/Sent Mail selected. (Success)
            //A00000017 OK [READ-ONLY] INBOX selected. (Success)
            CommonResponseRegexList.Add (new Regex (@"^(?<metadata>.*)" + ImapCommandNumRegexStr + @"(?<cmd>OK |NO |BAD )(?<type>\[[\w\-]+\] )(?<redact>.+)(?<selected> selected)(?<rest>.*)$", rxOptions));

            //A00000003 LIST "" ...
            //A00000004 XLIST "" ...
            CommonRequestRegexList.Add (new Regex (@"^" + ImapCommandNumRegexStr + @"(?<cmd>LIST |XLIST )(?<namespace>\S+ )(?<redact>.*)(?<end>[\r\n]+)$", rxOptions));

            //* XLIST (\HasNoChildren \Inbox) ...
            //* LIST (\HasNoChildren \Inbox) ...
            CommonResponseRegexList.Add (new Regex (@"^(?<star>\* )(?<cmd>LIST |XLIST )(?<flags>\([^\)]+\) )(?<redact>.*)(?<end>[\r\n]+)$", rxOptions));

            //A00000001 AUTHENTICATE XOAUTH2 ....
            var rx = new Regex (@"^" + ImapCommandNumRegexStr + @"(?<cmd>AUTHENTICATE )(?<type>\S+ )(?<redact>.*)(?<end>[\r\n]+)$", rxOptions);
            CommonRequestRegexList.Add (rx);
            AuthRequestRegexList.Add (rx);

            //A00000001 AUTHENTICATE PLAIN (can be multiline!!)
            rx = new Regex (@"^" + ImapCommandNumRegexStr + @"(?<cmd>AUTHENTICATE )(?<type>\S+)(?<end>[\r\n]+)$", rxOptions);
            AuthRequestRegexList.Add (rx);

            //A00000002 LOGIN jan.vilhuber@gmail.com ....
            rx = new Regex (@"^" + ImapCommandNumRegexStr + @"(?<cmd>LOGIN )(?<redact>.*)(?<end>[\r\n]*)$", rxOptions);
            CommonRequestRegexList.Add (rx);
            AuthRequestRegexList.Add (rx);

            //A00000001 OK jan.vilhuber@gmail.com authenticated (Success)
            CommonResponseRegexList.Add (new Regex (@"^(?<capabilities>.*)" + ImapCommandNumRegexStr + @"(?<cmd>OK |NO |BAD )(?<redact>.+)(?<authenticated> authenticated)(?<rest>.*)$", rxOptions));
        }

        private byte[] RedactLogData(bool isRequest, byte[] logData)
        {
            bool matched;
            string logString = Encoding.UTF8.GetString (logData);

            logString = RedactLogDataRegex (isRequest ? CommonRequestRegexList : CommonResponseRegexList, logString, out matched);
            if (!matched) {
                logString = RedactProtocolLogFunc (isRequest, logString);
            }
            return Encoding.UTF8.GetBytes (logString);
        }

        private void setPausingBasedOnAuth(bool isRequest, byte[] logData)
        {
            string logString = Encoding.UTF8.GetString (logData);
            // Try to pause any logging between when we see any sort of authentication command and the following command.
            if (isRequest) {
                if (pauseLogging) {
                    if (ImapCommandRx.IsMatch (logString)) {
                        pauseLogging = false;
                    }
                }
                // don't use else here, since there might be cases where we issue multiple 'AUTHENTICATE' commands in a row.
                // The previous if will turn of pausing, but the next for loop will turn pausing right back on, as we
                // need it to.
                foreach (var rx in AuthRequestRegexList) {
                    if (rx.IsMatch (logString)) {
                        pauseLogging = true;
                        break;
                    }
                }
            }
        }

        public static string RedactLogDataRegex(List<Regex> regexList, string logString, out bool matched)
        {
            matched = false;
            foreach (var rx in regexList) {
                // Find matches.
                logString = RedactLogDataRegex (rx, logString, out matched);
                if (matched) {
                    break;
                }
            }
            return logString;
        }

        private static string RedactLogDataRegex(Regex rx, string logString, out bool matched)
        {
            MatchCollection matches = rx.Matches (logString);
            matched = 0 != matches.Count;
            if (matched) {
                string s = string.Empty;
                foreach (Match match in matches) {
                    for (int i = 1; i < match.Groups.Count; i++) {
                        if (rx.GroupNameFromNumber (i).StartsWith ("redact")) {
                            s += "REDACTED";
                        } else {
                            s += match.Groups [i];
                        }
                    }
                }
                logString = s;
            }
            return logString;
        }

        public static string RedactLogDataRegex(List<Regex> regexList, string logString)
        {
            bool matched;
            return RedactLogDataRegex (regexList, logString, out matched);
        }

        #region IProtocolLogger implementation

        public void LogConnect (Uri uri)
        {
        }

        private void logBuffer (bool isRequest, byte[] buffer, int offset, int count)
        {
            byte[] logData = buffer.Skip (offset).Take (count).ToArray ();
            byte[] timestamp = Encoding.ASCII.GetBytes (String.Format ("{0:yyyy-MM-ddTHH:mm:ss.fffZ}: ", DateTime.UtcNow));
            byte[] prefix = isRequest ? Encoding.ASCII.GetBytes ("C: ") : Encoding.ASCII.GetBytes ("S: ");

            if (!_Enabled || null == RedactProtocolLogFunc) {
                return;
            }

            byte[] logRedactedBytes;
            if (isRequest && pauseLogging) {
                setPausingBasedOnAuth (isRequest, logData);
            }
            if (isRequest && pauseLogging) {
                logRedactedBytes = Encoding.UTF8.GetBytes ("Redacted Authentication line\n");
            } else {
                logRedactedBytes = RedactLogData (isRequest, logData);
            }
            setPausingBasedOnAuth (isRequest, logData); // do this after redaction, so we still log the actual AUTH command

            MemoryStream memBuf = isRequest ? RequestLogBuffer : ResponseLogBuffer;
            if (null != memBuf) {
                memBuf.Write (timestamp, 0, timestamp.Length);
                memBuf.Write (logPrefix, 0, logPrefix.Count ());
                memBuf.Write (prefix, 0, prefix.Count ());
                memBuf.Write (logRedactedBytes, 0, logRedactedBytes.Length);
            }

            CombinedLogBuffer.Write (timestamp, 0, timestamp.Length);
            CombinedLogBuffer.Write (logPrefix, 0, logPrefix.Count ());
            CombinedLogBuffer.Write (prefix, 0, prefix.Count ());
            CombinedLogBuffer.Write (logRedactedBytes, 0, logRedactedBytes.Length);
        }

        public void LogClient (byte[] buffer, int offset, int count)
        {
            logBuffer (true, buffer, offset, count);
        }

        public void LogServer (byte[] buffer, int offset, int count)
        {
            logBuffer (false, buffer, offset, count);
        }

        public void Dispose ()
        {
        }
        #endregion

        #region INcProtocolLogger implementation

        public byte[] GetRequestBuffer ()
        {
            return RequestLogBuffer.GetBuffer ();
        }

        public byte[] GetResponseBuffer ()
        {
            return ResponseLogBuffer.GetBuffer ();
        }

        public byte[] GetCombinedBuffer ()
        {
            return CombinedLogBuffer.GetBuffer ();
        }
        public void ResetBuffers ()
        {
            if (null != RequestLogBuffer) {
                RequestLogBuffer.Dispose ();
                RequestLogBuffer = null;
            }
            if (null != ResponseLogBuffer) {
                ResponseLogBuffer.Dispose ();
                ResponseLogBuffer = null;
            }
            if (null != CombinedLogBuffer && CombinedLogBuffer.Length > 0) {
                CombinedLogBuffer.Dispose ();
                CombinedLogBuffer = null;
            }
            CombinedLogBuffer = new MemoryStream ();
        }

        public void Start (RedactProtocolLogFuncDel func)
        {
            RequestLogBuffer = new MemoryStream ();
            ResponseLogBuffer = new MemoryStream ();
            RedactProtocolLogFunc = func;
            _Enabled = true;
        }

        public void Stop ()
        {
            _Enabled = false;
            RedactProtocolLogFunc = null;
            ResetBuffers ();
        }

        public void Stop (out byte[]RequestLog, out byte[] ResponseLog)
        {
            RequestLog = null != RequestLogBuffer ? RequestLogBuffer.GetBuffer () : null;
            ResponseLog = null != ResponseLogBuffer ? ResponseLogBuffer.GetBuffer () : null;
            Stop ();
        }

        public bool Enabled ()
        {
            return _Enabled;
        }

        #endregion
    }

    public class NcDebugProtocolLogger : IProtocolLogger
    {
        string LogPrefix;
        ulong LogModule;
        public NcDebugProtocolLogger (ulong logModule)
        {
            LogModule = logModule;
            LogPrefix = Log.ModuleString (logModule);
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

