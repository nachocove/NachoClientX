//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MailKit;
using System.IO;
using MailKit.Net.Imap;

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
}
