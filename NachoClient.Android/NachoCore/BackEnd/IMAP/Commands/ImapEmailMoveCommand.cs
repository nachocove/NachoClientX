//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;

namespace NachoCore.IMAP
{
    public class ImapEmailMoveCommand : ImapCommand
    {
        public ImapEmailMoveCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            var emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (BEContext.Account.Id, PendingSingle.ServerId);
            NcAssert.NotNull (emailMessage);
            var folderGuid = ImapProtoControl.ImapMessageFolderGuid (PendingSingle.ServerId);
            var emailUid = ImapProtoControl.ImapMessageUid (PendingSingle.ServerId);
            McFolder src = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.ParentId);
            NcAssert.Equals (folderGuid, src.ImapGuid);
            var srcFolder = Client.GetFolder (src.ServerId, Cts.Token);
            NcAssert.NotNull (srcFolder);
            McFolder dst = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.DestParentId);
            var dstFolder = Client.GetFolder (dst.ServerId, Cts.Token);
            NcAssert.NotNull (dstFolder);

            srcFolder.Open (FolderAccess.ReadWrite, Cts.Token);
            UniqueId? newUid = srcFolder.MoveTo (emailUid, dstFolder, Cts.Token);
            if (null != newUid) {
                emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    target.ServerId = ImapProtoControl.MessageServerId (dst, (UniqueId)newUid);
                    return true;
                });
            } else {
                // FIXME How do we determine the new ID? This can happen with servers that don't support UIDPLUS.
            }
            // FIXME Need to do fixup stuff in pending. Are there API's for that?

            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPMOVSUC");
        }
    }
}
