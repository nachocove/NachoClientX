﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Model;
using NachoCore.Utils;
using System.IO;
using System.Linq;
using MailKit;
using System;

namespace NachoCore.IMAP
{
    public class ImapFetchAttachmentCommand : ImapCommand, ITransferProgress
    {
        public ImapFetchAttachmentCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            PendingSingle = pending;
            pending.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            NcResult result = null;
            result = FetchAttachment (PendingSingle);
            if (result.isInfo ()) {
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, result);
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPATTSUCC");
            } else {
                NcAssert.True (result.isError ());
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, result);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSUCCHARD0");
            }
        }

        private NcResult FetchAttachment (McPending pending)
        {
            McEmailMessage email = McEmailMessage.QueryByServerId<McEmailMessage> (BEContext.Account.Id, pending.ServerId);
            if (null == email) {
                Log.Error (Log.LOG_IMAP, "Could not find email for ServerId {0}", pending.ServerId);
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
            }
            var attachment = McAttachment.QueryById<McAttachment> (pending.AttachmentId);
            if (null == attachment) {
                Log.Error (Log.LOG_IMAP, "Could not find attachment for Id {0}", pending.AttachmentId);
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
            }
            var folder = McFolder.QueryByImapGuid (BEContext.Account.Id, ImapProtoControl.ImapMessageFolderGuid (email.ServerId)).FirstOrDefault();
            if (null == folder) {
                Log.Error (Log.LOG_IMAP, "Could not find folder with ImapGuid {0}", ImapProtoControl.ImapMessageFolderGuid (email.ServerId));
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
            }
            var mailKitFolder = GetOpenMailkitFolder (folder);
            mailKitFolder.SetStreamContext (ImapProtoControl.ImapMessageUid (email.ServerId), attachment.GetFilePath ());
            try {
                mailKitFolder.GetBodyPart (ImapProtoControl.ImapMessageUid (email.ServerId), attachment.FileReference, Cts.Token, this);
            } catch (Exception e) {
                Log.Error (Log.LOG_IMAP, "Could not GetBodyPart: {0}", e);
                attachment.DeleteFile ();
                mailKitFolder.UnsetStreamContext ();
                return NcResult.Error (NcResult.SubKindEnum.Error_AttDownloadFailed);
            }
            attachment.Truncated = false;
            attachment.UpdateSaveFinish ();
            return NcResult.Info (NcResult.SubKindEnum.Info_AttDownloadUpdate);
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
