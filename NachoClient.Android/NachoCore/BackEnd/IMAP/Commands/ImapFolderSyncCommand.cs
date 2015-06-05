//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using MailKit;
using NachoCore;
using NachoCore.Model;

namespace NachoCore.IMAP
{
    public class ImapFolderSyncCommand : ImapCommand
    {
        public ImapFolderSyncCommand (IBEContext beContext) : base (beContext)
        {
        }

        protected override Event ExecuteCommand ()
        {
            // Right now, we rely on MailKit's FolderCache so access is synchronous.
            IEnumerable<IMailFolder> folderList;
            // On startup, we just asked the server for a list of folder (via Client.Authenticate()).
            // An optimization might be to keep a timestamp since the last authenticate OR last Folder Sync, and
            // skip the GetFolders if it's semi-recent (seconds).
            lock (Client.SyncRoot) {
                if (Client.PersonalNamespaces.Count == 0) {
                    Log.Error (Log.LOG_IMAP, "No personal namespaces");
                    return Event.Create ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD0");
                }
                // TODO Should we loop over all namespaces here? Typically there appears to be only one.
                folderList = Client.GetFolders (Client.PersonalNamespaces[0], false, Cts.Token);
            }

            if (null == folderList) {
                Log.Error (Log.LOG_IMAP, "Could not refresh folder list");
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD3");
            }

            // Process all incoming folders. Create or update them
            List<string> foldernames = new List<string> (); // Keep track of folder names, so we can compare later.
            bool added_or_changed = false;
            foreach (var mailKitFolder in folderList) {
                foldernames.Add (mailKitFolder.FullName);

                if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Inbox)) {
                    if (CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2, mailKitFolder.Name, true)) {
                        added_or_changed = true;
                    }
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Sent)) {
                    if (CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultSent_5, mailKitFolder.Name, true)) {
                        added_or_changed = true;
                    }
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Drafts)) {
                    if (CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDrafts_3, mailKitFolder.Name, true)) {
                        added_or_changed = true;
                    }
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Trash)) {
                    if (CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDeleted_4, mailKitFolder.Name, true)) {
                        added_or_changed = true;
                    }
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Junk)) {
                    if (CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, mailKitFolder.Name, false)) {
                        added_or_changed = true;
                    }
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Archive)) {
                    if (CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, McFolder.ARCHIVE_DISPLAY_NAME, false)) {
                        added_or_changed = true;
                    }
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.All)) {
                    if (CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, mailKitFolder.Name, false)) {
                        added_or_changed = true;
                    }
                }
                else {
                    if ("notes" == mailKitFolder.Name.ToLower ()) {
                        if (CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultNotes_10, mailKitFolder.Name, true)) {
                            added_or_changed = true;
                        }
                    } else {
                        if (CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, mailKitFolder.Name, false)) {
                            added_or_changed = true;
                        }
                    }
                }
            }

            // Compare the incoming folders to the ones we know about. Delete any that disappeared.
            foreach (var folder in McFolder.QueryByIsClientOwned (BEContext.Account.Id, false)) {
                if (!foldernames.Contains (folder.ServerId)) {
                    Log.Info (Log.LOG_IMAP, "Deleting folder {0} due to disappeared from server");
                    folder.Delete ();
                    added_or_changed = true;
                }
            }

            if (added_or_changed) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
            }

            var protocolState = BEContext.ProtocolState;
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.AsLastFolderSync = DateTime.UtcNow;
                return true;
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPFSYNCSUC");
        }
    }
}
