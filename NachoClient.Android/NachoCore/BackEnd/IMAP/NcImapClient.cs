//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MailKit;
using System.IO;
using MailKit.Net.Imap;
using System.Linq;
using System.Text;

namespace NachoCore.IMAP
{
    public class NcImapClient : MailKit.Net.Imap.ImapClient
    {
        public NcMailKitProtocolLogger MailKitProtocolLogger { get; private set; }

        public NcImapClient () : base(getLogger())
        {
            MailKitProtocolLogger = ProtocolLogger as NcMailKitProtocolLogger;
        }

        protected override ImapFolder CreateImapFolder (ImapFolderConstructorArgs args)
        {
            return new NcImapFolder (args);
        }

        private static IProtocolLogger getLogger ()
        {
            //return new NcMailKitProtocolLogger ("IMAP");
            //return new NcDebugProtocolLogger ();
            return new NullProtocolLogger ();
        }
    }

    public class NcImapFolder : ImapFolder
    {
        public NcImapFolder (ImapFolderConstructorArgs args) : base(args) {}

        private NcImapFolderStreamContext StreamContext { get; set; }
        public void SetStreamContext (UniqueId uid, string filePath)
        {
            StreamContext = new NcImapFolderStreamContext() {
                uid = uid,
                FilePath = filePath,
            };
        }

        public void UnsetStreamContext()
        {
            StreamContext = null;
        }

        protected override Stream CreateStream (UniqueId? uid, string section, int offset, int length)
        {
            // TODO Use a file-base stream, instead of memory. Need to figure out which file to open, and how
            // to pass that information in here.
            string uidString;
            if (uid.HasValue) {
                uidString = uid.Value.ToString ();
            } else {
                uidString = "none";
            }
            if (null != StreamContext && StreamContext.uid.ToString () != uidString) {
                Log.Error (Log.LOG_IMAP, "StreamContext UID {0} does not match uid {1}", StreamContext.uid, uidString);
            }
            Stream stream;
            if (null == StreamContext || StreamContext.uid.ToString () != uidString) {
                stream = base.CreateStream (uid, section, offset, length);
            } else {
                stream = new FileStream (StreamContext.FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            }
            return stream;
        }

        protected override Stream CommitStream (Stream stream, UniqueId uid)
        {
            if (null == StreamContext) {
                return base.CommitStream (stream, uid);
            } else {
                UnsetStreamContext ();
                return stream;
            }
        }

        public class NcImapFolderStreamContext
        {
            public UniqueId uid;
            public string FilePath;

            public NcImapFolderStreamContext ()
            {}
        }
    }

    public class NcDebugProtocolLogger : IProtocolLogger
    {
        #region IProtocolLogger implementation
        public void LogConnect (System.Uri uri)
        {
            Log.Info (Log.LOG_IMAP, "Connect {0}", uri);
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
            Log.Info (Log.LOG_IMAP, "IMAP: {0}:{1}", isRequest ? "C: " : "S: ", Encoding.UTF8.GetString (logData));
        }
    }
}
