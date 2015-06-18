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
        public ImapFolderUpdateCommand (IBEContext beContext, ImapClient imap, McPending pending) : base (beContext, imap)
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

            var mailKitFolder = Client.GetFolder (folder.ServerId, Cts.Token);
            if (null == mailKitFolder) {
                Log.Error (Log.LOG_IMAP, "Could not get folder on server");
                throw new NcImapCommandFailException (Event.Create ((uint)SmEvt.E.HardFail, "IMAPFOLCREFAIL"), NcResult.WhyEnum.MissingOnServer);
            }
            mailKitFolder.Open (FolderAccess.ReadWrite, Cts.Token);
            mailKitFolder.Rename (encapsulatingFolder, PendingSingle.DisplayName);

            if (CreateOrUpdateFolder (mailKitFolder, PendingSingle.Folder_Type, PendingSingle.DisplayName, folder.IsDistinguished, out folder)) {
                UpdateImapSetting (mailKitFolder, folder);
                // TODO Do applyCommand stuff here
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_FolderUpdateSucceeded));
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPFUPSUC");
            } else {
                Log.Error (Log.LOG_IMAP, "Folder {0} should have been changed but wasn't", mailKitFolder.FullName);
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPFUPHRD");
            }
        }
    }
}

