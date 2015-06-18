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
    public interface INcProtocolLogger {
        byte[] GetResponseBuffer ();
        byte[] GetRequestBuffer ();
        byte[] GetCombinedBuffer ();
        void ResetBuffers();
        bool Enabled ();
        void Start ();
        void Stop ();
        void Stop (out byte[]RequestLog, out byte[] ResponseLog);
    }

    public class MailKitProtocolLogger : IProtocolLogger, INcProtocolLogger
    {
        private byte[] logPrefix;
        private MemoryStream RequestLogBuffer;
        private MemoryStream ResponseLogBuffer;
        private MemoryStream CombinedLogBuffer;
        private bool _Enabled;

        public MailKitProtocolLogger (string prefix)
        {
            logPrefix = Encoding.ASCII.GetBytes(prefix + " ");

            ResetBuffers ();
            _Enabled = false;
        }

        #region IProtocolLogger implementation

        public void LogConnect (Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException ("uri");

            Log.Info (Log.LOG_SYS, "Connected to {0}", uri);
        }

        private void logBuffer (bool isRequest, byte[] buffer, int offset, int count)
        {
            if (!_Enabled) {
                return;
            }

            byte[] prefix = isRequest ? Encoding.ASCII.GetBytes("C: ") : Encoding.ASCII.GetBytes("S: ");

            MemoryStream memBuf = isRequest ? RequestLogBuffer : ResponseLogBuffer;
            byte[] timestamp = Encoding.ASCII.GetBytes (String.Format ("{0:yyyy-MM-ddTHH:mm:ss.fffZ}: ", DateTime.UtcNow));
            if (null != memBuf) {
                memBuf.Write (timestamp, 0, timestamp.Length);
                memBuf.Write (logPrefix, 0, logPrefix.Count ());
                memBuf.Write (prefix, 0, prefix.Count ());
                memBuf.Write (buffer, offset, count);
            }

            CombinedLogBuffer.Write (timestamp, 0, timestamp.Length);
            CombinedLogBuffer.Write (logPrefix, 0, logPrefix.Count ());
            CombinedLogBuffer.Write (prefix, 0, prefix.Count ());
            CombinedLogBuffer.Write (buffer, offset, count);
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

        public void Start ()
        {
            RequestLogBuffer = new MemoryStream ();
            ResponseLogBuffer = new MemoryStream ();
            _Enabled = true;
        }

        public void Stop ()
        {
            _Enabled = false;
            ResetBuffers ();
        }

        public void Stop (out byte[]RequestLog, out byte[] ResponseLog)
        {
            RequestLog = null != RequestLogBuffer ? RequestLogBuffer.GetBuffer () : null;
            ResponseLog = null != ResponseLogBuffer ? ResponseLogBuffer.GetBuffer () : null;
            _Enabled = false;
            ResetBuffers ();
        }

        public bool Enabled ()
        {
            return _Enabled;
        }

        #endregion
    }
}

