//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MailKit;
using System.IO;
using MailKit.Net.Imap;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System;
using System.Threading;
using NachoCore.Model;
using MailKit.Security;
using System.Runtime.InteropServices;

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
            //return new NcDebugProtocolLogger (Log.LOG_IMAP);
            return new NullProtocolLogger ();
        }

        public void Connect (McServer server, CancellationToken Token)
        {
            var ipAddresses = Dns.GetHostAddresses (server.Host);
            Socket socket = null;

            for (int i = 0; i < ipAddresses.Length; i++) {
                socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
//                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 2000);
//                SetTcpKeepAlive (socket, 250, 2000);
                try {
                    Token.ThrowIfCancellationRequested ();
                    socket.Connect (ipAddresses[i], server.Port);
                    break;
                } catch (OperationCanceledException) {
                    socket.Dispose ();
                    throw;
                } catch {
                    socket.Dispose ();

                    if (i + 1 == ipAddresses.Length)
                        throw;
                }
            }

            if (socket == null) {
                throw new IOException (string.Format ("Failed to resolve host: {0}", server.Host));
            }
            SecureSocketOptions options = SecureSocketOptions.Auto;
            Connect (socket, server.Host, server.Port, options, Token);
        }

        private static void SetTcpKeepAlive(Socket socket, uint keepaliveTime, uint keepaliveInterval)
        {
            /* the native structure
        struct tcp_keepalive {
        ULONG onoff;
        ULONG keepalivetime;
        ULONG keepaliveinterval;
        };
        */

            // marshal the equivalent of the native structure into a byte array
            uint dummy = 0;
            byte[] inOptionValues = new byte[Marshal.SizeOf(dummy) * 3];
            BitConverter.GetBytes((uint)(keepaliveTime)).CopyTo(inOptionValues, 0);
            BitConverter.GetBytes((uint)keepaliveTime).CopyTo(inOptionValues, Marshal.SizeOf(dummy));
            BitConverter.GetBytes((uint)keepaliveInterval).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);

            // write SIO_VALS to Socket IOControl
            socket.IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
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
