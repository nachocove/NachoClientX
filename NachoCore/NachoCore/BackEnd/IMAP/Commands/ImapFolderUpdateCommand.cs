//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;
using MailKit.Net.Imap;

namespace NachoCore.IMAP
{
    public class ImapFolderUpdateCommand : ImapCommand
    {
        public ImapFolderUpdateCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispatched ();
        }

        protected override Event ExecuteCommand ()
        {
            // TODO Not sure if this is the right thing to do. Do we loop over all namespaces if there's more than 1? How do we pick?
            FolderNamespace imapNameSpace = Client.PersonalNamespaces [0];

            McFolder folder = McFolder.QueryByServerId<McFolder> (AccountId, PendingSingle.ServerId);

            IMailFolder encapsulatingFolder;
            IMailFolder mailKitFolder;
            if (McFolder.AsRootServerId != PendingSingle.DestParentId) {
                encapsulatingFolder = Client.GetFolder (PendingSingle.DestParentId, Cts.Token);
            } else {
                encapsulatingFolder = Client.GetFolder (imapNameSpace.Path);
            }

            mailKitFolder = Client.GetFolder (folder.ServerId, Cts.Token);
            NcAssert.NotNull (mailKitFolder);
            mailKitFolder.Open (FolderAccess.ReadWrite, Cts.Token);
            var oldName = mailKitFolder.FullName;
            mailKitFolder.Rename (encapsulatingFolder, PendingSingle.DisplayName, Cts.Token);

            if (CreateOrUpdateFolder (mailKitFolder, PendingSingle.Folder_Type, PendingSingle.DisplayName, folder.IsDistinguished, false, out folder)) {
                UpdateImapSetting (mailKitFolder, ref folder);
                // TODO Do applyCommand stuff here
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_FolderUpdateSucceeded));
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPFUPSUC");
            } else {
                mailKitFolder.Rename (encapsulatingFolder, oldName, Cts.Token);
                Log.Error (Log.LOG_IMAP, "Folder should have been changed but wasn't");
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPFUPHRD");
            }
        }
    }
}

