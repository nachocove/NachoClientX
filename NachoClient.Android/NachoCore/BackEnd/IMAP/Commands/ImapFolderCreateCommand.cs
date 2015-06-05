//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;

namespace NachoCore.IMAP
{
    public class ImapFolderCreateCommand : ImapCommand
    {
        public ImapFolderCreateCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            NcAssert.NotNull (Client.PersonalNamespaces);
            NcAssert.True (Client.PersonalNamespaces.Count >= 1);

            // TODO Not sure if this is the right thing to do. Do we loop over all namespaces if there's more than 1? How do we pick?
            FolderNamespace imapNameSpace = Client.PersonalNamespaces [0];

            string folderPath;
            if (McFolder.AsRootServerId != PendingSingle.ParentId) {
                folderPath = PendingSingle.ParentId + imapNameSpace.DirectorySeparator + PendingSingle.DisplayName;
            } else {
                folderPath = PendingSingle.DisplayName;
            }
            var newFolder = CreateFolderInNamespace (imapNameSpace, folderPath);

            if (CreateOrUpdateFolder (newFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, newFolder.Name, false)) {
                // TODO do some ApplyCommand stuff here
                Log.Info (Log.LOG_IMAP, "Created folder {0}", newFolder.FullName);
            }

            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_FolderCreateSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPFCRSUC");
        }

        private IMailFolder CreateFolderInNamespace (FolderNamespace imapNameSpace, string name)
        {
            IMailFolder folder = null;
            var encapsulatingFolder = Client.GetFolder (imapNameSpace.Path);
            folder = encapsulatingFolder.Create (name, true);
            NcAssert.NotNull (folder);
            return folder;
        }
    }
}

