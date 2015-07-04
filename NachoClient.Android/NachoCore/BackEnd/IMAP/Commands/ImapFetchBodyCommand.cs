//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MimeKit;
using MailKit;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Model;
using System;

namespace NachoCore.IMAP
{
    public class ImapFetchBodyCommand : ImapCommand, ITransferProgress
    {
        public ImapFetchBodyCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            pending.MarkDispached ();
            PendingSingle = pending;
            //RedactProtocolLogFunc = RedactProtocolLog;
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            return logData;
        }

        protected override Event ExecuteCommand ()
        {
            NcResult result = null;
            result = ProcessPending (PendingSingle);
            if (result.isInfo ()) {
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, result);
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPBDYSUCC");
            } else {
                NcAssert.True (result.isError ());
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, result);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPBDYHRD0");
            }
        }

        private NcResult ProcessPending (McPending pending)
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
                return FetchOneBody (pending);

            default:
                NcAssert.True (false, string.Format ("ItemOperations: inappropriate McPending Operation {0}", pending.Operation));
                break;
            }
            return NcResult.Error ("Unknown operation");
        }

        private NcResult FetchOneBody (McPending pending)
        {
            McEmailMessage email = McAbstrItem.QueryByServerId<McEmailMessage> (BEContext.Account.Id, pending.ServerId);
            if (null == email) {
                Log.Error (Log.LOG_IMAP, "Could not find email for {0}", pending.ServerId);
                return NcResult.Error ("Unknown email ServerId");
            }

            NcResult result = null;
            McFolder folder = McFolder.QueryByServerId (BEContext.Account.Id, pending.ParentId);
            var mailKitFolder = GetOpenMailkitFolder (folder);

            McBody body;
            if (0 == email.BodyId) {
                body = new McBody () {
                    AccountId = BEContext.Account.Id,
                };
            } else {
                body = McBody.QueryById<McBody> (email.BodyId);
            }
            var uid = ImapProtoControl.ImapMessageUid (pending.ServerId);
            MimeMessage imapbody = null;
            mailKitFolder.SetStreamContext (uid, body.GetFilePath ());
            try {
                imapbody = mailKitFolder.GetMessage (uid, Cts.Token, this);
            } catch (Exception e) {
                body.Delete ();
                mailKitFolder.UnsetStreamContext ();
                throw;
            }
            if (null == imapbody) {
                Log.Error (Log.LOG_IMAP, "ImapFetchBodyCommand: no message found");
                result = NcResult.Error ("No Body found");
            } else {
                if (imapbody.Body.ContentType.Matches ("multipart", "*")) {
                    body.BodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
                } else if (imapbody.Body.ContentType.Matches ("text", "*")) {
                    if (imapbody.Body.ContentType.Matches ("text", "html")) {
                        body.BodyType = McAbstrFileDesc.BodyTypeEnum.HTML_2;
                    } else if (imapbody.Body.ContentType.Matches ("text", "plain")) {
                        body.BodyType = McAbstrFileDesc.BodyTypeEnum.PlainText_1;
                    } else {
                        Log.Error (Log.LOG_IMAP, "Unhandled text subtype {0}", imapbody.Body.ContentType.MediaSubtype);
                        result = NcResult.Error ("Unhandled text subtype");
                    }
                } else {
                    Log.Error (Log.LOG_IMAP, "Unhandled mime subtype {0}", imapbody.Body.ContentType.ToString ());
                    result = NcResult.Error ("Unhandled mimetype subtype");
                }
            }
            if (null == result) {
                email.BodyId = body.Id;
                body.Truncated = false;
                body.UpdateSaveFinish ();
                result = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded);
            } else {
                body.Delete ();
                email.BodyId = 0;
            }
            email.Update ();
            return result;
        }

        #region ITransferProgress implementation

        public void Report (long bytesTransferred, long totalSize)
        {
            Log.Info (Log.LOG_IMAP, "Download progress: bytesTransferred {0} totalSize {1}", bytesTransferred, totalSize);
        }

        public void Report (long bytesTransferred)
        {
            Log.Info (Log.LOG_IMAP, "Download progress: bytesTransferred {0}", bytesTransferred);
        }

        #endregion
    }
}
