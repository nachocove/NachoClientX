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
    public class ImapFetchBodyCommand : ImapCommand
    {
        ImapFolder _folder { get; set; }
        public ImapFetchBodyCommand (IBEContext beContext, ImapClient imap, McPending pending) : base (beContext, imap)
        {
            pending.MarkDispached ();
            PendingSingle = pending;
            _folder = null;
        }

        public override void Execute (NcStateMachine sm)
        {
            try {
                lock (Client.SyncRoot) {
                    var result = ProcessPending (sm, PendingSingle);
                    if (result.isInfo ()) {
                        PendingSingle.ResolveAsSuccess (BEContext.ProtoControl, result);
                        sm.PostEvent ((uint)SmEvt.E.Success, "IMAPBDYSUCC");
                    } else if (result.isError ()) {
                        PendingSingle.ResolveAsHardFail (BEContext.ProtoControl, result);
                        sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPBDYHRD0");
                    }
                }
            } catch (OperationCanceledException) {
                PendingSingle.ResolveAsCancelled ();
                return;
            } catch (ServiceNotConnectedException) {
                PendingSingle.ResolveAsDeferred (BEContext.ProtoControl, DateTime.UtcNow,
                    NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError,
                        NcResult.WhyEnum.ServerError));
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: Client is not connected.");
                sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.ReConn, "IMAPBDYRECONN");
                return;
            } catch (InvalidOperationException e) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: {0}", e);
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed));
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPBDYHRD1");
                return;
            } catch (Exception e) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: Unexpected exception: {0}", e);
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed));
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPBDYHRD2");
                return;
            }
        }

        private UniqueId AsUniqueId(string serverId)
        {
            uint x = UInt32.Parse (serverId);
            return new UniqueId(x);
        }

        private ImapFolder GetOpenedFolder(string serverId)
        {
            if (null == _folder || _folder.FullName != serverId) {
                Log.Info (Log.LOG_IMAP, "Opening folder {0}", serverId);
                _folder = Client.GetFolder (serverId, Cts.Token) as ImapFolder;
                NcAssert.NotNull(_folder);
                _folder.Open (FolderAccess.ReadOnly);
            }
            return _folder;
        }

        private NcResult ProcessPending(NcStateMachine sm, McPending pending)
        {
            McPending.Operations op;
            if (null == pending) {
                // If there is no pending, then we are doing an email prefetch.
                op = McPending.Operations.EmailBodyDownload;
            } else {
                op = pending.Operation;
            }
            switch (op) {
            case McPending.Operations.EmailBodyDownload:
                return FetchOneBody (sm, pending);

            default:
                NcAssert.True (false, string.Format ("ItemOperations: inappropriate McPending Operation {0}", pending.Operation));
                break;
            }
            return NcResult.Error ("Unknown operation");
        }

        private NcResult FetchOneBody(NcStateMachine sm, McPending pending)
        {
            McEmailMessage email = McAbstrItem.QueryByServerId<McEmailMessage> (BEContext.Account.Id, pending.ServerId);
            NcResult result;

            var folder = GetOpenedFolder (pending.ParentId);

            MimeMessage imapbody = folder.GetMessage (AsUniqueId(pending.ServerId), Cts.Token);
            if (null == imapbody) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: no message found");
                email.BodyId = 0;
                result = NcResult.Error ("No Body found");
            } else {
                McAbstrFileDesc.BodyTypeEnum bodyType;
                // FIXME Getting the 'body' string is inefficient and wasteful.
                //   Perhaps use the WriteTo method on the Body, write to a file,
                //   then open the file and pass that stream to UpdateData/InsertFile?
                string bodyAsString;
                if (imapbody.Body.ContentType.Matches ("multipart", "*")) {
                    bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
                    bodyAsString = imapbody.Body.ToString ();
                } else if (imapbody.Body.ContentType.Matches ("text", "*")) {
                    if (imapbody.Body.ContentType.Matches ("text", "html")) {
                        bodyType = McAbstrFileDesc.BodyTypeEnum.HTML_2;
                        bodyAsString = imapbody.HtmlBody;
                    } else if (imapbody.Body.ContentType.Matches ("text", "plain")) {
                        bodyType = McAbstrFileDesc.BodyTypeEnum.PlainText_1;
                        bodyAsString = imapbody.TextBody;
                    } else {
                        Log.Error (Log.LOG_IMAP, "Unhandled text subtype {0}", imapbody.Body.ContentType.MediaSubtype);
                        sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPBDYPLAIN");
                        return NcResult.Error ("Unhandled text subtype");
                    }
                } else {
                    Log.Error (Log.LOG_IMAP, "Unhandled mime subtype {0}", imapbody.Body.ContentType.ToString ());
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPBDYMIME");
                    return NcResult.Error ("Unhandled mimetype subtype");
                }

                McBody body;
                if (0 == email.BodyId) {
                    body = McBody.InsertFile (pending.AccountId, bodyType, bodyAsString); 
                    email.BodyId = body.Id;
                } else {
                    body = McBody.QueryById<McBody> (email.BodyId);
                    body.UpdateData (bodyAsString);
                }
                body.BodyType = bodyType;
                body.Truncated = false;
                body.FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete;
                body.FileSize = bodyAsString.Length;
                body.FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Actual;
                body.Update ();
                result = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded);
            }
            email.Update ();
            return result;
        }
    }
}
