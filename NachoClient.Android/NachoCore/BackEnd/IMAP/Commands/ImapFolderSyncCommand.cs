//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using MailKit;
using NachoCore;
using NachoCore.Model;
using MailKit.Net.Imap;
using System.Text.RegularExpressions;
using System.Linq;

namespace NachoCore.IMAP
{
    public class ImapFolderSyncCommand : ImapCommand
    {
        public ImapFolderSyncCommand (IBEContext beContext) : base (beContext)
        {
        }

        private bool FindAndCreateFolder(ref IList<IMailFolder> folderList, ref List<string> foldernames, string debugTag,
            Func <IMailFolder, Tuple<ActiveSync.Xml.FolderHierarchy.TypeCode, string, bool>> action)
        {
            List<int> toRemoveIndexList = new List<int> ();
            bool added_or_changed = false;
            for (var i =0; i<folderList.Count; i++) {
                var mailKitFolder = folderList [i];
                Cts.Token.ThrowIfCancellationRequested ();

                Tuple<ActiveSync.Xml.FolderHierarchy.TypeCode, string, bool> result = action (mailKitFolder);
                if (null != result) {
                    McFolder folder;
                    if (CreateOrUpdateFolder (mailKitFolder, result.Item1, result.Item2, result.Item3, false, out folder)) {
                        added_or_changed = true;
                        // TODO do ApplyCommand stuff here
                    }
                    if (null != folder) {
                        if (UpdateImapSetting (mailKitFolder, ref folder)) {
                            // Don't set added_or_changed, as that would trigger a Info_FolderSetChanged indication, and the set didn't change.
                            // Strategy will notice that modseq and/or noselect etc has changed, and resync.
                        }
                        foldernames.Add (mailKitFolder.FullName);
                        toRemoveIndexList.Add (i);
                    }
                }
            }
            // remove from the end of the list, otherwise the index changes
            // (i.e. say we want to remove 1 and 6: remove 1 and item 6 becomes 5. Remove 6 and boom).
            toRemoveIndexList.Reverse ();
            foreach (var i in toRemoveIndexList) {
                folderList.RemoveAt (i);
            }
            return added_or_changed;
        }

        protected override Event ExecuteCommand ()
        {
            McProtocolState protocolState = BEContext.ProtocolState;

            // On startup, we just asked the server for a list of folder (via Client.Authenticate()).
            // An optimization might be to keep a timestamp since the last authenticate OR last Folder Sync, and
            // skip the GetFolders if it's semi-recent (seconds).
            if (Client.PersonalNamespaces.Count == 0) {
                Log.Error (Log.LOG_IMAP, "ImapFolderSyncCommand: No personal namespaces");
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD0");
            }
            bool subscribedOnly = false;
            IList<IMailFolder> folderList = Client.GetFolders (Client.PersonalNamespaces [0], subscribedOnly, Cts.Token).ToList ();
            if (null == folderList) {
                Log.Error (Log.LOG_IMAP, "ImapFolderSyncCommand: Could not refresh folder list");
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD3");
            }

            // Process all incoming folders. Create or update them
            List<string> foldernames = new List<string> (); // Keep track of folder names, so we can compare later.
            bool added_or_changed = false;
            bool haveSent = false;
            bool haveDraft = false;
            bool haveTrash = false;
            bool haveNotes = false;

            // First, look for the Inbox
            added_or_changed |= FindAndCreateFolder (ref folderList, ref foldernames, "Inbox", mailKitFolder => {
                if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Inbox) || Client.Inbox == mailKitFolder) {
                    return Tuple.Create<ActiveSync.Xml.FolderHierarchy.TypeCode, string, bool> (ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2, mailKitFolder.Name, true);
                }
                return null;
            });

            if (protocolState.ImapSyncRung > 0) {
                // Then, look for all 'directory' (hasChildren) folders.
                added_or_changed |= FindAndCreateFolder (ref folderList, ref foldernames, "Dir Folder", mailKitFolder => {
                    if (mailKitFolder.Attributes.HasFlag (FolderAttributes.HasChildren)) {
                        return Tuple.Create<ActiveSync.Xml.FolderHierarchy.TypeCode, string, bool> (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, mailKitFolder.Name, false);
                    }
                    return null;
                });

                // second, look for some folders we don't want to misidentify
                added_or_changed |= FindAndCreateFolder (ref folderList, ref foldernames, "Special Folder", mailKitFolder => {
                    ActiveSync.Xml.FolderHierarchy.TypeCode folderType;
                    bool isDistinguished;
                    if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Sent)) {
                        folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultSent_5;
                        isDistinguished = true;
                        haveSent = true;
                    } else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Drafts)) {
                        folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDrafts_3;
                        isDistinguished = true;
                        haveDraft = true;
                    } else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Trash)) {
                        folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDeleted_4;
                        isDistinguished = true;
                        haveTrash = true;
                    } else {
                        return null;
                    }
                    return Tuple.Create<ActiveSync.Xml.FolderHierarchy.TypeCode, string, bool> (folderType, mailKitFolder.Name, isDistinguished);
                });

                // look again and process the rest.
                added_or_changed |= FindAndCreateFolder (ref folderList, ref foldernames, "General Folder", mailKitFolder => {
                    ActiveSync.Xml.FolderHierarchy.TypeCode folderType;
                    bool isDistinguished;

                    if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Junk)) {
                        folderType = NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12;
                        isDistinguished = false;
                    } else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Archive)) {
                        folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12;
                        isDistinguished = false;
                    } else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.All)) {
                        folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12;
                        isDistinguished = false;
                    } else {
                        var folderName = mailKitFolder.Name;
                        if (!haveNotes && McFolder.MaybeNotesFolder (protocolState.ImapServiceType, folderName)) {
                            folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultNotes_10;
                            isDistinguished = true;
                            haveNotes = true;
                        } else if (!haveSent && McFolder.MaybeSentFolder (protocolState.ImapServiceType, folderName)) {
                            folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultSent_5;
                            isDistinguished = true;
                            haveSent = true;
                        } else if (!haveTrash && McFolder.MaybeTrashFolder (protocolState.ImapServiceType, folderName)) {
                            folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDeleted_4;
                            isDistinguished = true;
                            haveTrash = true;
                        } else if (!haveDraft && McFolder.MaybeDraftFolder (protocolState.ImapServiceType, folderName)) {
                            folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDrafts_3;
                            isDistinguished = true;
                            haveDraft = true;
                        } else {
                            folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12;
                            isDistinguished = false;
                        }
                    }
                    return Tuple.Create<ActiveSync.Xml.FolderHierarchy.TypeCode, string, bool> (folderType, mailKitFolder.Name, isDistinguished);
                });

                if (folderList.Any ()) {
                    Log.Error (Log.LOG_IMAP, "Not all incoming folders processed!");
                }

                // Compare the incoming folders to the ones we know about. Delete any that disappeared.
                foreach (var folder in McFolder.QueryByIsClientOwned (AccountId, false)) {
                    Cts.Token.ThrowIfCancellationRequested ();
                    if (!foldernames.Contains (folder.ServerId)) {
                        Log.Info (Log.LOG_IMAP, "ImapFolderSyncCommand: Deleting folder {0} due to disappearance from server", folder.ImapFolderNameRedacted ());
                        // TODO Do applyCommand stuff here
                        // Delete folder and everything in and under it.
                        folder.Delete ();
                        added_or_changed = true;
                    }
                }
            }
            Finish (added_or_changed, ref protocolState);
            return Event.Create ((uint)SmEvt.E.Success, "IMAPFSYNCSUC");
        }

        private void Finish (bool added_or_changed, ref McProtocolState protocolState)
        {
            Cts.Token.ThrowIfCancellationRequested ();

            if (added_or_changed) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
            }

            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.AsLastFolderSync = DateTime.UtcNow;  // FIXME: Rename AsLastFolderSync to be generic.
                return true;
            });
        }
    }
}
