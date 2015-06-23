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

namespace NachoCore.IMAP
{
    public class ImapFolderSyncCommand : ImapCommand
    {
        private List<Regex> RegexList;

        public ImapFolderSyncCommand (IBEContext beContext, NcImapClient imap) : base (beContext, imap)
        {
            RedactProtocolLogFunc = RedactProtocolLog;
            RegexList = new List<Regex> ();
            RegexList.Add (new Regex (@"^(?<num>\w+)(?<space1>\s)(?<cmd>UID MOVE )(?<uid>\d+)(?<space1>\s)(?<redact>.*)$", NcMailKitProtocolLogger.rxOptions));
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            return NcMailKitProtocolLogger.RedactLogDataRegex (RegexList, logData);
        }

        protected override Event ExecuteCommand ()
        {
            // Right now, we rely on MailKit's FolderCache so access is synchronous.
            IEnumerable<IMailFolder> folderList;
            // On startup, we just asked the server for a list of folder (via Client.Authenticate()).
            // An optimization might be to keep a timestamp since the last authenticate OR last Folder Sync, and
            // skip the GetFolders if it's semi-recent (seconds).
            if (Client.PersonalNamespaces.Count == 0) {
                Log.Error (Log.LOG_IMAP, "No personal namespaces");
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD0");
            }
            // TODO Should we loop over all namespaces here? Typically there appears to be only one.
            folderList = Client.GetFolders (Client.PersonalNamespaces[0], false, Cts.Token);


            if (null == folderList) {
                Log.Error (Log.LOG_IMAP, "Could not refresh folder list");
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD3");
            }

            // Process all incoming folders. Create or update them
            List<string> foldernames = new List<string> (); // Keep track of folder names, so we can compare later.
            bool added_or_changed = false;
            foreach (var mailKitFolder in folderList) {
                foldernames.Add (mailKitFolder.FullName);

                ActiveSync.Xml.FolderHierarchy.TypeCode folderType;
                bool isDistinguished;

                if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Inbox)) {
                    folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2;
                    isDistinguished = true;
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Sent)) {
                    folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultSent_5;
                    isDistinguished = true;
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Drafts)) {
                    folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDrafts_3;
                    isDistinguished = true;
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Trash)) {
                    folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDeleted_4;
                    isDistinguished = true;
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Junk)) {
                    folderType = NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12;
                    isDistinguished = false;
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Archive)) {
                    folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12;
                    isDistinguished = false;
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.All)) {
                    folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12;
                    isDistinguished = false;
                }
                else {
                    if ("notes" == mailKitFolder.Name.ToLower ()) {
                        folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultNotes_10;
                        isDistinguished = true;
                    } else {
                        folderType = ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12;
                        isDistinguished = false;
                    }
                }

                // FIXME: Catch errors here, so that an error for one folder doesn't blow up the entire FolderSync

                McFolder folder;
                if (!mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect)) {
                    mailKitFolder.Open (FolderAccess.ReadOnly);
                }
                if (CreateOrUpdateFolder (mailKitFolder, folderType, mailKitFolder.Name, isDistinguished, out folder)) {
                    added_or_changed = true;
                    // TODO do ApplyCommand stuff here
                }
                if (null != folder) {
                    if (UpdateImapSetting (mailKitFolder, folder)) {
                        // Don't set added_or_changed, as that would trigger a Info_FolderSetChanged indication, and the set didn't change.
                        // Strategy will notice that modseq and/or noselect etc has changed, and resync.
                        Log.Info (Log.LOG_IMAP, "Folder {0} imap settings changed", folder.ImapFolderNameRedacted());
                    }
                } else {
                    Log.Error (Log.LOG_IMAP, "No folder returned from CreateOrUpdateFolder!");
                }
            }

            // Compare the incoming folders to the ones we know about. Delete any that disappeared.
            foreach (var folder in McFolder.QueryByIsClientOwned (BEContext.Account.Id, false)) {
                if (!foldernames.Contains (folder.ServerId)) {
                    Log.Info (Log.LOG_IMAP, "Deleting folder {0} due to disappeared from server", folder.ImapFolderNameRedacted());
                    // TODO Do applyCommand stuff here
                    // Delete folder and everything in and under it.
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
                target.AsLastFolderSync = DateTime.UtcNow;  // FIXME: Rename AsLastFolderSync to be generic.
                return true;
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPFSYNCSUC");
        }
    }
}
