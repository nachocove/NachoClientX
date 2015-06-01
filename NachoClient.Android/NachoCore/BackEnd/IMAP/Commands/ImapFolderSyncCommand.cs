//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NachoCore.Utils;
using System.Threading;
using MimeKit;
using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;
using MailKit.Security;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.IMAP
{
    public class ImapFolderSyncCommand : ImapCommand
    {
        public ImapFolderSyncCommand (IBEContext beContext, ImapClient imap) : base (beContext, imap)
        {
        }

        public override void Execute (NcStateMachine sm)
        {
            // Right now, we rely on MailKit's FolderCache so access is synchronous.
            IEnumerable<IMailFolder> folderList;
            try {
                // On startup, we just asked the server for a list of folder (via Client.Authenticate()).
                // An optimization might be to keep a timestamp since the last authenticate OR last Folder Sync, and
                // skip the GetFolders if it's semi-recent (seconds).
                lock(Client.SyncRoot) {
                    if (Client.PersonalNamespaces.Count == 0) {
                        Log.Error (Log.LOG_IMAP, "No personal namespaces");
                        sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD0");
                        return;
                    }
                    // TODO Should we loop over all namespaces here? Typically there appears to be only one.
                    folderList = Client.GetFolders (Client.PersonalNamespaces[0], false, Cts.Token);
                }
            } catch (OperationCanceledException) {
                // Not going to happen until we nix CancellationToken.None.
                Log.Info (Log.LOG_IMAP, "ImapFolderSyncCommand: Cancelled");
                return;
            } catch (ServiceNotConnectedException) {
                Log.Error (Log.LOG_IMAP, "ImapFolderSyncCommand: Client is not connected.");
                sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.ReConn, "IMAPFSYNCRECONN");
                return;
            } catch (InvalidOperationException e) {
                Log.Error (Log.LOG_IMAP, "ImapFolderSyncCommand: {0}", e);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD1");
                return;
            }
            catch (Exception e) {
                Log.Error (Log.LOG_IMAP, "GetFolders: Unexpected exception: {0}", e);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD2");
                return;
            }

            if (null == folderList) {
                Log.Error (Log.LOG_IMAP, "Could not refresh folder list");
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPFSYNCHRD3");
                return;
            }


            // Process all incoming folders. Create or update them
            List<string> foldernames = new List<string> (); // Keep track of folder names, so we can compare later.
            foreach (var mailKitFolder in folderList) {
                foldernames.Add (mailKitFolder.FullName);

                if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Inbox)) {
                    CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2, mailKitFolder.Name, true);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Sent)) {
                    CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultSent_5, mailKitFolder.Name, true);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Drafts)) {
                    CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDrafts_3, mailKitFolder.Name, true);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Trash)) {
                    CreateOrUpdateFolder (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDeleted_4, mailKitFolder.Name, true);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Junk)) {
                    CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, mailKitFolder.Name, false);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.Archive)) {
                    CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, McFolder.ARCHIVE_DISPLAY_NAME, false);
                }
                else if (mailKitFolder.Attributes.HasFlag (FolderAttributes.All)) {
                    CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, mailKitFolder.Name, false);
                }
                else {
                    if ("notes" == mailKitFolder.Name.ToLower ()) {
                        CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultNotes_10, mailKitFolder.Name, true);
                    } else {
                        CreateOrUpdateFolder (mailKitFolder, NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12, mailKitFolder.Name, false);
                    }
                }
            }

            // Compare the incoming folders to the ones we know about. Delete any that disappeared.
            foreach (var folder in McFolder.QueryByIsClientOwned (BEContext.Account.Id, false)) {
                if (!foldernames.Contains (folder.ServerId)) {
                    Log.Info (Log.LOG_IMAP, "Deleting folder {0} due to disappeared from server");
                    folder.Delete ();
                }
            }
            sm.PostEvent ((uint)SmEvt.E.Success, "IMAPFSYNCSUC");
        }

        private string parentId(IMailFolder mailKitFolder)
        {
            return null != mailKitFolder.ParentFolder && string.Empty != mailKitFolder.ParentFolder.FullName ?
                mailKitFolder.ParentFolder.FullName : McFolder.AsRootServerId;
        }

        protected void CreateOrUpdateFolder (IMailFolder mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode folderType, string folderDisplayName, bool isDisinguished)
        {
            McFolder existing;
            if (isDisinguished) {
                existing = McFolder.GetDistinguishedFolder (BEContext.Account.Id, folderType);
            } else {
                existing = McFolder.GetUserFolders (BEContext.Account.Id, folderType, parentId(mailKitFolder), mailKitFolder.Name).SingleOrDefault ();
            }

            if ((null != existing) && (existing.ImapUidValidity < mailKitFolder.UidValidity)) {
                Log.Info (Log.LOG_IMAP, "Deleting folder {0} due to UidValidity ({1} < {2})", mailKitFolder.FullName, existing.ImapUidValidity, mailKitFolder.UidValidity.ToString ());
                existing.Delete ();
                existing = null;
            }

            if (null == existing) {
                // Add it
                var created = McFolder.Create (BEContext.Account.Id, false, false, isDisinguished, parentId(mailKitFolder), mailKitFolder.FullName, mailKitFolder.Name, folderType);
                created.ImapUidValidity = mailKitFolder.UidValidity;
                created.Insert ();
            } else if (existing.ServerId != mailKitFolder.FullName ||
                       existing.DisplayName != folderDisplayName ||
                       existing.ImapUidValidity != mailKitFolder.UidValidity) {
                // update.
                existing = existing.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ServerId = mailKitFolder.FullName;
                    target.DisplayName = folderDisplayName;
                    target.ImapUidValidity = mailKitFolder.UidValidity;
                    return true;
                });
                return;
            }
        }
    }

}
