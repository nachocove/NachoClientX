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
        public ImapEmailMoveCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            var emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (BEContext.Account.Id, PendingSingle.ServerId);
            if (null == emailMessage) {
                Log.Error (Log.LOG_IMAP, "No Email matches id {0}.", PendingSingle.ServerId);
                throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPEMMOVHARD0"), NcResult.WhyEnum.NotSpecified);
            }
            McFolder src = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.ParentId);
            if (null == src) {
                Log.Error (Log.LOG_IMAP, "No folder matches id {0}.", PendingSingle.ParentId);
                throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPEMMOVHARD1"), NcResult.WhyEnum.NotSpecified);
            }
            McFolder dst = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.DestParentId);
            if (null == src) {
                Log.Error (Log.LOG_IMAP, "No folder matches id {0}.", PendingSingle.DestParentId);
                throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPEMMOVHARD2"), NcResult.WhyEnum.NotSpecified);
            }

            MoveEmail (Client, emailMessage, src, dst, Cts.Token);

            // FIXME Need to do fixup stuff in pending. Are there API's for that?
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPEMMOVSUC");
        }

        public static void MoveEmail(ImapClient Client, McEmailMessage emailMessage, McFolder src, McFolder dst, CancellationToken Token)
        {
            UniqueId? newUid;
            lock (Client.SyncRoot) {
                var folderGuid = ImapProtoControl.ImapMessageFolderGuid (emailMessage.ServerId);
                var emailUid = ImapProtoControl.ImapMessageUid (emailMessage.ServerId);
                if (folderGuid != src.ImapGuid) {
                    Log.Error (Log.LOG_IMAP, "folder UIDVALIDITY does not match.");
                    throw new NcImapCommandRetryException (Event.Create ((uint)ImapProtoControl.ImapEvt.E.FolderSync, "IMAPEMMOVUID"));
                }
                var srcFolder = Client.GetFolder (src.ServerId, Token);
                if (null == srcFolder) {
                    Log.Error (Log.LOG_IMAP, "Could not Get imap src folder");
                    throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPEMMOVHARD3"), NcResult.WhyEnum.MissingOnServer);
                }
                var dstFolder = Client.GetFolder (dst.ServerId, Token);
                if (null == dstFolder) {
                    Log.Error (Log.LOG_IMAP, "Could not Get imap dst folder");
                    throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPEMMOVHARD4"), NcResult.WhyEnum.MissingOnServer);
                }

                srcFolder.Open (FolderAccess.ReadWrite, Token);
                newUid = srcFolder.MoveTo (emailUid, dstFolder, Token);
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
