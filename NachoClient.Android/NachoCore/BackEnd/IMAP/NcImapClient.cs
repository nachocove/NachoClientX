﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MailKit;
using System.IO;
using MailKit.Net.Imap;
using System;

namespace NachoCore.IMAP
{
    public class NcImapClient : ImapClient
    {
        public NcMailKitProtocolLogger MailKitProtocolLogger { get; private set; }

        public NcImapClient () : base (getLogger ())
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
            //return new NcDebugProtocolLogger (Log.LOG_IMAP);
            return new NullProtocolLogger ();
        }
    }

    public class NcImapFolder : ImapFolder
    {
        public NcImapFolder (ImapFolderConstructorArgs args) : base (args)
        {
        }

        NcImapFolderStreamContext _StreamContext;

        NcImapFolderStreamContext StreamContext {
            get {
                return _StreamContext;
            }
            set {
                if (null != _StreamContext) {
                    _StreamContext.Dispose ();
                }
                _StreamContext = value;
            }
        }

        public void SetStreamContext (UniqueId uid, string filePath, bool deleteFile = true)
        {
            StreamContext = new NcImapFolderStreamContext (uid, filePath, deleteFile);
        }

        public void UnsetStreamContext ()
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
            if (null != StreamContext && StreamContext.Uid.ToString () != uidString) {
                Log.Error (Log.LOG_IMAP, "StreamContext UID {0} does not match uid {1}", StreamContext.Uid, uidString);
            }
            Stream stream;
            if (null == StreamContext || StreamContext.Uid.ToString () != uidString) {
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

        public class NcImapFolderStreamContext : IDisposable
        {
            public UniqueId Uid { get; protected set; }

            public string FilePath { get; protected set; }

            public bool DeleteFile { get; set; }

            public NcImapFolderStreamContext (UniqueId uid, string filePath, bool deleteFile)
            {
                Uid = uid;
                FilePath = filePath;
                DeleteFile = deleteFile;
            }

            #region IDisposable implementation

            public void Dispose ()
            {
                if (DeleteFile && !string.IsNullOrEmpty (FilePath)) {
                    File.Delete (FilePath);
                }
            }

            #endregion
        }
    }
}
