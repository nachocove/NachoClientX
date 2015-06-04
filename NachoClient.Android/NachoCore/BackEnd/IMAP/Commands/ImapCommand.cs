//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NachoCore.Utils;
using System.Threading;
using MimeKit;
using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;
using MailKit.Security;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.IMAP
{
    public class ImapCommand : NcCommand
    {
        protected ImapClient Client { get; set; }

        public ImapCommand (IBEContext beContext) : base (beContext)
        {
            Client = ((ImapProtoControl)BEContext.ProtoControl).ImapClient;
        }

        // MUST be overridden by subclass.
        protected virtual Event ExecuteCommand ()
        {
            NcAssert.True (false);
            return null;
        }

        public override void Cancel ()
        {
            base.Cancel ();
            // FIXME - not a long term soln. There are issues with MailKit and cancellation.
            lock (Client.SyncRoot) {
            }
        }

        public override void Execute (NcStateMachine sm)
        {
            NcTask.Run (() => {
                try {
                    if (!Client.IsConnected || !Client.IsAuthenticated) {
                        var authy = new ImapAuthenticateCommand (BEContext);
                        authy.ConnectAndAuthenticate ();
                    }
                    var evt = ExecuteCommand ();
                    // In the no-exception case, ExecuteCommand is resolving McPending.
                    sm.PostEvent (evt);
                } catch (OperationCanceledException) {
                    Log.Info (Log.LOG_IMAP, "OperationCanceledException");
                    ResolveAllDeferred ();
                    // No event posted to SM if cancelled.
                } catch (ServiceNotConnectedException) {
                    // FIXME - this needs to feed into NcCommStatus, not loop forever.
                    Log.Info (Log.LOG_IMAP, "ServiceNotConnectedException");
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.ReDisc, "IMAPCONN");
                } catch (ServiceNotAuthenticatedException) {
                    Log.Info (Log.LOG_IMAP, "ServiceNotAuthenticatedException");
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTH");
                } catch (IOException ex) {
                    Log.Info (Log.LOG_IMAP, "IOException: {0}", ex.ToString ());
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)SmEvt.E.TempFail, "IMAPIO");
                } catch (InvalidOperationException ex) {
                    Log.Error (Log.LOG_IMAP, "InvalidOperationException: {0}", ex.ToString ());
                    ResolveAllFailed (NcResult.WhyEnum.ProtocolError);
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPHARD1");
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "Exception : {0}", ex.ToString ());
                    ResolveAllFailed (NcResult.WhyEnum.Unknown);
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPHARD2");
                }
            }, "ImapCommand");
        }

        protected IMailFolder GetOpenMailkitFolder(McFolder folder, FolderAccess access = FolderAccess.ReadOnly)
        {
            IMailFolder mailKitFolder;
            mailKitFolder = Client.GetFolder (folder.ServerId);
            if (null == mailKitFolder) {
                return null;
            }
            if (FolderAccess.None == mailKitFolder.Open (access, Cts.Token)) {
                return null;
            }
            return mailKitFolder;
        }
    }
}
