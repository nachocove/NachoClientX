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
        List<McPending> PendingList;
        ImapFolder _folder { get; set; }
        public ImapFetchBodyCommand (IBEContext beContext, ImapClient imap, McPending pending) : base (beContext, imap)
        {
            PendingSingle = pending;
            _folder = null;
        }

        public override void Execute (NcStateMachine sm)
        {
            try {
                lock (Client.SyncRoot) {
                    ProcessPending (sm, PendingSingle);
                }
            } catch (InvalidOperationException e) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: {0}", e);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPBDYHRD1");
                return;
            } catch (Exception e) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: Unexpected exception: {0}", e);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPBDYHRD3");
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

        private void ProcessPending(NcStateMachine sm, McPending pending)
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
                FetchOneBody (sm, pending);
                break;
            case McPending.Operations.AttachmentDownload:
                break;
            default:
                NcAssert.True (false, string.Format ("ItemOperations: inappropriate McPending Operation {0}", pending.Operation));
                break;
            }
        }

        private void FetchOneBody(NcStateMachine sm, McPending pending)
        {
            pending.MarkDispached ();
            McEmailMessage email = McAbstrItem.QueryByServerId<McEmailMessage> (BEContext.Account.Id, pending.ServerId);

            var folder = GetOpenedFolder (pending.ParentId);

            MimeMessage imapbody = folder.GetMessage (AsUniqueId(pending.ServerId), Cts.Token);
            if (null == imapbody) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: no message found");
                email.BodyId = 0;
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
                        return;
                    }
                } else {
                    Log.Error (Log.LOG_IMAP, "Unhandled multipart subtype {0}", imapbody.Body.ContentType.MediaSubtype);
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPBDYMULT");
                    return;
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
            }
            email.Update ();
        }

        private void FetchOneAttachment(McPending pending)
        {
            // FIXME implement me
            // pending.MarkDispached ();
            // var attachment = McAbstrObject.QueryById<McAttachment> (pending.AttachmentId);
        }

    }
}
