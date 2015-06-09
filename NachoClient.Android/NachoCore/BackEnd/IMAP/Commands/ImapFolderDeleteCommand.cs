//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;

namespace NachoCore.IMAP
{
    public class ImapFolderDeleteCommand : ImapCommand
    {
        public ImapFolderDeleteCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            McFolder folder = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.ServerId);
            var mailKitFolder = Client.GetFolder (folder.ServerId, Cts.Token);
            if (null == mailKitFolder) {
                Log.Error (Log.LOG_IMAP, "Could not get folder on server");
                throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPFOLCREFAIL"), NcResult.WhyEnum.MissingOnServer);
            }
            if (mailKitFolder.IsOpen) {
                mailKitFolder.Close (false, Cts.Token); // rfc4549 Sec 3.c.2: If the action is to delete a mailbox (DELETE), make sure that the mailbox is closed first
            }
            mailKitFolder.Delete (Cts.Token);

            // Blow folder (and subitems) away
            folder.Delete ();

            BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_FolderDeleteSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPFDESUC");
        }
    }
}

