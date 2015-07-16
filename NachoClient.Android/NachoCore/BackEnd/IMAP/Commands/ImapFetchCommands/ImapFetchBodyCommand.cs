//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MimeKit;
using MailKit;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Model;
using System.IO;
using MimeKit.IO;
using MimeKit.IO.Filters;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore.IMAP
{
    public class ImapFetchBodyCommand : ImapFetchCommand
    {
        public ImapFetchBodyCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            pending.MarkDispached ();
            PendingSingle = pending;
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

            NcResult result;
            McFolder folder = McFolder.QueryByServerId (BEContext.Account.Id, pending.ParentId);
            var mailKitFolder = GetOpenMailkitFolder (folder);

            UniqueId uid = ImapProtoControl.ImapMessageUid (email.ServerId);
            McAbstrFileDesc.BodyTypeEnum bodyType;
            result = messageBodyPart (uid, mailKitFolder, out bodyType);
            if (!result.isOK ()) {
                return result;
            }
            BodyPart part = result.GetValue<BodyPart> ();

            var tmp = NcModel.Instance.TmpPath (BEContext.Account.Id);
            mailKitFolder.SetStreamContext (uid, tmp);
            McBody body;
            if (0 == email.BodyId) {
                body = new McBody () {
                    AccountId = BEContext.Account.Id,
                    BodyType = bodyType,
                };
                body.Insert ();
            } else {
                body = McBody.QueryById<McBody> (email.BodyId);
            }

            try {
                Stream st = mailKitFolder.GetStream (uid, part.PartSpecifier, Cts.Token);
                var path = body.GetFilePath ();
                using (var bodyFile = new FileStream (path, FileMode.OpenOrCreate, FileAccess.Write)) {
                    st.CopyTo(bodyFile);
                }
                body.Truncated = false;
                body.UpdateSaveFinish ();

                email.BodyId = body.Id;
                email.Update ();

                result = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded);
            } catch (ImapCommandException ex) {
                Log.Warn (Log.LOG_IMAP, "ImapCommandException: {0}", ex.Message);
                // TODO Need to narrow this down. Pull in latest MailKit and make it compile.
                // the message doesn't exist. Delete it locally.
                Log.Warn (Log.LOG_IMAP, "ImapFetchBodyCommand: no message found. Deleting local copy");
                body.DeleteFile ();
                body.Delete ();
                email.Delete ();
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                result = NcResult.Error ("No Body found");
            }
            mailKitFolder.UnsetStreamContext ();
            return result;
        }

        private NcResult messageBodyPart(UniqueId uid, IMailFolder mailKitFolder, out McAbstrFileDesc.BodyTypeEnum bodyType)
        {
            NcResult result;
            var UidList = new List<UniqueId> ();
            UidList.Add (uid);
            MessageSummaryItems flags = MessageSummaryItems.BodyStructure | MessageSummaryItems.UniqueId;
            var isummary = mailKitFolder.Fetch (UidList, flags, Cts.Token);
            if (null == isummary || isummary.Count < 1) {
                Log.Error (Log.LOG_IMAP, "Could not get summary for uid {0}", uid);
            }
            var summary = isummary[0] as MessageSummary;
            var part = summary.Body;

            if (part.ContentType.Matches ("multipart", "*")) {
                bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
            } else if (part.ContentType.Matches ("text", "*")) {
                if (part.ContentType.Matches ("text", "html")) {
                    bodyType = McAbstrFileDesc.BodyTypeEnum.HTML_2;
                } else if (part.ContentType.Matches ("text", "plain")) {
                    bodyType = McAbstrFileDesc.BodyTypeEnum.PlainText_1;
                } else {
                    bodyType = McAbstrFileDesc.BodyTypeEnum.None;
                    return NcResult.Error (string.Format ("Unhandled text subtype {0}", part.ContentType.MediaSubtype));
                }
            } else {
                bodyType = McAbstrFileDesc.BodyTypeEnum.None;
                return NcResult.Error (string.Format ("Unhandled mime subtype {0}", part.ContentType.MediaSubtype));
            }
            result = NcResult.OK ();
            result.Value = part;
            return result;
        }
    }
}
