//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;
using System.Threading;
using MailKit.Net.Imap;

namespace NachoCore.IMAP
{
    public class ImapEmailMoveCommand : ImapCommand
    {
        public ImapEmailMoveCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            var emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (BEContext.Account.Id, PendingSingle.ServerId);
            NcAssert.NotNull (emailMessage);
            McFolder src = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.ParentId);
            NcAssert.NotNull (src);
            McFolder dst = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.DestParentId);
            NcAssert.NotNull (dst);

            MoveEmail (emailMessage, src, dst, Cts.Token);

            // FIXME Need to do fixup stuff in pending. Are there API's for that?
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPMOVSUC");
        }

        public void MoveEmail(McEmailMessage emailMessage, McFolder src, McFolder dst, CancellationToken Token)
        {
            UniqueId? newUid;
            lock (Client.SyncRoot) {
                try {
                    ProtocolLoggerStart ();
                    var folderGuid = ImapProtoControl.ImapMessageFolderGuid (emailMessage.ServerId);
                    var emailUid = ImapProtoControl.ImapMessageUid (emailMessage.ServerId);
                    NcAssert.Equals (folderGuid, src.ImapGuid);
                    var srcFolder = Client.GetFolder (src.ServerId, Token);
                    NcAssert.NotNull (srcFolder);
                    var dstFolder = Client.GetFolder (dst.ServerId, Token);
                    NcAssert.NotNull (dstFolder);

                    srcFolder.Open (FolderAccess.ReadWrite, Token);
                    newUid = srcFolder.MoveTo (emailUid, dstFolder, Token);
                } catch {
                    throw;
                } finally {
                    ProtocolLoggerStopAndPostTelemetry ();
                }
            }
            if (null != newUid && newUid.HasValue && 0 != newUid.Value.Id) {
                emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    target.ServerId = ImapProtoControl.MessageServerId (dst, (UniqueId)newUid);
                    return true;
                });
            } else {
                // FIXME How do we determine the new ID? This can happen with servers that don't support UIDPLUS.
            }
        }
    }
}
