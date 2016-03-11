//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;
using System.Collections.Generic;
using MailKit.Net.Imap;

namespace NachoCore.IMAP
{
    public class ImapFolderCreateCommand : ImapCommand
    {
        public ImapFolderCreateCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispatched ();
            //RedactProtocolLogFunc = RedactProtocolLog;
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            return logData;
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

            McFolder folder;
            if (CreateOrUpdateFolder (newFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, newFolder.Name, false, false, out folder)) {
                // TODO do some ApplyCommand stuff here
                // FIXME This is especially needed the first time you archive an email: there will be two
                //   pendings in the queue. One to create the folder and one to move the message. The second one will
                //   not have the right destId, because that folder doesn't yet exist, so we fail in the Move Command.
                Log.Info (Log.LOG_IMAP, "Created folder {0}", newFolder.FullName);
            }
            if (folder == null) {
                Log.Error (Log.LOG_IMAP, "Could not create new folder");
                newFolder.Delete (Cts.Token);
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPFCRHARD");
            }
            UpdateImapSetting (newFolder, ref folder);

            var applyFolderCreate = new ApplyCreateFolder (AccountId) {
                PlaceholderId = PendingSingle.ServerId,
                FinalServerId = newFolder.FullName,
            };
            applyFolderCreate.ProcessServerCommand ();

            BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
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

        private class ApplyCreateFolder : NcApplyServerCommand
        {
            public string FinalServerId { set; get; }

            public string PlaceholderId { set; get; }

            public ApplyCreateFolder (int accountId) : base (accountId)
            {
            }

            protected override List<McPending.ReWrite> ApplyCommandToPending (McPending pending, 
                out McPending.DbActionEnum action,
                out bool cancelDelta)
            {
                // TODO - need a McPending method that acts on ALL ServerId fields.
                action = McPending.DbActionEnum.DoNothing;
                cancelDelta = false;
                if (null != pending.ServerId && pending.ServerId == PlaceholderId) {
                    pending.ServerId = FinalServerId;
                    action = McPending.DbActionEnum.Update;
                }
                if (null != pending.DestParentId && pending.DestParentId == PlaceholderId) {
                    pending.DestParentId = FinalServerId;
                    action = McPending.DbActionEnum.Update;
                }
                if (null != pending.ParentId && pending.ParentId == PlaceholderId) {
                    pending.ParentId = FinalServerId;
                    action = McPending.DbActionEnum.Update;
                }
                return null;
            }

            protected override void ApplyCommandToModel ()
            {
                var created = McFolder.QueryByServerId<McFolder> (AccountId, PlaceholderId);
                if (null != created) {
                    created = created.UpdateWithOCApply<McFolder> ((record) => {
                        var target = (McFolder)record;
                        target.ServerId = FinalServerId;
                        target.IsAwaitingCreate = false;
                        return true;
                    });
                    var account = McAccount.QueryById<McAccount> (AccountId);
                    var protocolState = McProtocolState.QueryByAccountId<McProtocolState> (account.Id).SingleOrDefault ();
                    var folders = McFolder.QueryByParentId (AccountId, PlaceholderId);
                    foreach (var child in folders) {
                        child.UpdateWithOCApply<McFolder> ((record) => {
                            var target = (McFolder)record;
                            target.ParentId = FinalServerId;
                            target.AsFolderSyncEpoch = protocolState.AsFolderSyncEpoch;
                            return true;
                        });
                    }
                }
            }
        }
    }
}

