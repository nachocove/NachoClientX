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
        public ImapFolderUpdateCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
            //RedactProtocolLogFunc = RedactProtocolLog;
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            return logData;
        }

        protected override Event ExecuteCommand ()
        {
            // TODO Not sure if this is the right thing to do. Do we loop over all namespaces if there's more than 1? How do we pick?
            FolderNamespace imapNameSpace = Client.PersonalNamespaces [0];

            McFolder folder = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.ServerId);

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
            mailKitFolder.Rename (encapsulatingFolder, PendingSingle.DisplayName);

            if (CreateOrUpdateFolder (mailKitFolder, PendingSingle.Folder_Type, PendingSingle.DisplayName, folder.IsDistinguished, out folder)) {
                UpdateImapSetting (mailKitFolder, ref folder);
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

