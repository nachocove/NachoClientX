//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;

namespace NachoCore.IMAP
{
    public class ImapFolderUpdateCommand : ImapCommand
    {
        public ImapFolderUpdateCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            // TODO Not sure if this is the right thing to do. Do we loop over all namespaces if there's more than 1? How do we pick?
            FolderNamespace imapNameSpace = Client.PersonalNamespaces [0];

            McFolder folder = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.ServerId);

            IMailFolder encapsulatingFolder;
            if (McFolder.AsRootServerId != PendingSingle.DestParentId) {
                encapsulatingFolder = Client.GetFolder (PendingSingle.DestParentId, Cts.Token);
            } else {
                encapsulatingFolder = Client.GetFolder (imapNameSpace.Path);
            }

            var imapFolder = Client.GetFolder (folder.ServerId, Cts.Token);
            NcAssert.NotNull (imapFolder);
            imapFolder.Open (FolderAccess.ReadWrite, Cts.Token);
            imapFolder.Rename (encapsulatingFolder, PendingSingle.DisplayName);

            if (CreateOrUpdateFolder (imapFolder, PendingSingle.Folder_Type, PendingSingle.DisplayName, folder.IsDistinguished, out folder)) {
                UpdateImapSetting (imapFolder, folder);
                // TODO Do applyCommand stuff here
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_FolderUpdateSucceeded));
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPFUPSUC");
            } else {
                Log.Error (Log.LOG_IMAP, "Folder {0} should have been changed but wasn't", imapFolder.FullName);
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPFUPHRD");
            }
        }
    }
}

