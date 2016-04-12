//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MailKit;
using System.IO;
using MailKit.Net.Imap;
using System;
using NachoCore.Model;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MailKit.Security;

namespace NachoCore.IMAP
{
    public class NcImapClient : ImapClient
    {
        public NcMailKitProtocolLogger MailKitProtocolLogger { get; private set; }

        public bool DOA { get; set; }

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

        public static Socket GetSocket (McServer server, int connectTimeout, int timeout, CancellationToken cancellationToken)
        {
            var ipAddresses = Dns.GetHostAddresses (server.Host);
            Socket socket = null;

            for (int i = 0; i < ipAddresses.Length; i++) {
                socket = new Socket (ipAddresses [i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try {
                    cancellationToken.ThrowIfCancellationRequested ();
                    IAsyncResult result = socket.BeginConnect(ipAddresses[i], server.Port, (r) => {
                        try {
                            socket.EndConnect(r);
                        }
                        catch (SocketException) { }
                        catch (ObjectDisposedException) { }
                    }, null);

                    bool success = result.AsyncWaitHandle.WaitOne(connectTimeout, true);
                    if (!success || !socket.Connected)
                    {
                        try {
                            socket.Close();
                        } catch {}
                        throw new SocketException();
                    }
                    socket.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    socket.SendTimeout = timeout;
                    socket.ReceiveTimeout = timeout;
                    return socket;
                } catch (OperationCanceledException) {
                    socket.Dispose ();
                    throw;
                } catch {
                    socket.Dispose ();

                    if (i + 1 == ipAddresses.Length)
                        throw;
                }
            }
            return null;
        }

        public void Connect (McServer server, int connectTimeout, int timeout, CancellationToken cancellationToken)
        {
            var socket = NcImapClient.GetSocket (server, connectTimeout, timeout, cancellationToken);
            if (null == socket) {
                Log.Error (Log.LOG_IMAP, "Could not open socket to {0}:{1}", server.Host, server.Port);
                throw new SocketException ();
            }
            // the parent class now owns the socket, so it's responsible for closing, freeing, etc.
            Connect (socket, server.Host, server.Port, SecureSocketOptions.SslOnConnect, cancellationToken);
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
            // a sanity check. Don't bother with the sanity check if we're not passed a valid uid. Some servers just don't seem to.
            if (null != StreamContext && uid.HasValue && uid.Value.ToString () != StreamContext.Uid.ToString ()) {
                Log.Error (Log.LOG_IMAP, "StreamContext UID {0} does not match uid {1}", StreamContext.Uid, uid.Value.ToString ());
            }
            Stream stream;
            if (null == StreamContext) {
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
