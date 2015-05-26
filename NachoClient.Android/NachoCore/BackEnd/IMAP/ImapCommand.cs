//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Threading;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Model;
using MailKit.Security;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.IMAP
{
    public class ImapCommand : IImapCommand
    {
        protected IBEContext BEContext;
        protected ImapClient Client { get; set; }
        public CancellationTokenSource cToken { get; protected set; }

        public async virtual void Execute (NcStateMachine sm)
        {
        }

        public async virtual void Cancel ()
        {
        }

        protected void CreateOrUpdateDistinguished (MailKit.IMailFolder mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode folderType)
        {
            // FIXME mailKitFolder == null should be considered a delete from the server.
            if (null == mailKitFolder) {
                return;
            }
            var existing = McFolder.GetDistinguishedFolder (BEContext.Account.Id, folderType);
            if (null == existing) {
                // Just add it.
                var created = new McFolder () {
                    AccountId = BEContext.Account.Id,
                    ServerId = mailKitFolder.FullName,
                    ParentId = McFolder.AsRootServerId,
                    DisplayName = mailKitFolder.Name,
                    Type = folderType,
                    ImapUidValidity = mailKitFolder.UidValidity,
                };
                created.Insert ();
            } else {
                // check & update.
                if (existing.AsSyncEpoch != mailKitFolder.UidValidity) {
                    // FIXME flush and re-sync folder contents.
                }
                existing = existing.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ServerId = mailKitFolder.FullName;
                    target.DisplayName = mailKitFolder.Name;
                    target.ImapUidValidity = mailKitFolder.UidValidity;
                    return true;
                });
            }
        }
    }

    public class ImapFolderSyncCommand : ImapCommand
    {
        public ImapFolderSyncCommand (IBEContext beContext, ImapClient imap)
        {
            cToken = new CancellationTokenSource ();
            BEContext = beContext;
            Client = imap;
        }

        public async override void Execute (NcStateMachine sm)
        {
            // Right now, we rely on MailKit's FolderCache so access is synchronous.
            CreateOrUpdateDistinguished (Client.Inbox, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            foreach (var special in Enum.GetValues (typeof (MailKit.SpecialFolder))) {
                try {
                    var specialValue = (MailKit.SpecialFolder)special;
                    var mailKitFolder = Client.GetFolder (specialValue);
                    switch (specialValue) {
                    case MailKit.SpecialFolder.Sent:
                        CreateOrUpdateDistinguished (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultSent_5);
                        break;
                    case MailKit.SpecialFolder.Drafts:
                        // FIXME - is IMAP drafts usable as a shared drafts folder?
                        CreateOrUpdateDistinguished (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDrafts_3);
                        break;
                    case MailKit.SpecialFolder.Trash:
                        CreateOrUpdateDistinguished (mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultDeleted_4);
                        break;
                    default:
                        // FIXME All, Archive, Flagged, Junk.
                        // FIXME http://tools.ietf.org/html/rfc6154
                        break;
                    }
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "Could not find special folder {0}", special.ToString ());
                }
                sm.PostEvent ((uint)SmEvt.E.Success, "IMAPFSYNCSUC");
            }
        }

        public async override void Cancel ()
        {
        }
    }

    public class ImapAuthenticateCommand : ImapCommand
    {

        public ImapAuthenticateCommand (IBEContext beContext, ImapClient imap)
        {
            cToken = new CancellationTokenSource ();
            BEContext = beContext;
            Client = imap;
        }

        public async override void Execute (NcStateMachine sm)
        {
            try {
                if (Client.IsConnected) {
                    await Client.DisconnectAsync (false, cToken.Token);
                }
                await Client.ConnectAsync (BEContext.Server.Host, BEContext.Server.Port, true, cToken.Token).ConfigureAwait (false);
                // FIXME: since we don't have an OAuth2 token, disable the XOAUTH2 authentication mechanism.
                Client.AuthenticationMechanisms.Remove ("XOAUTH2");
                await Client.AuthenticateAsync (BEContext.Cred.Username, BEContext.Cred.GetPassword (), cToken.Token).ConfigureAwait (false);
                // FIXME MailKit builds an inaccessible folder cache on connect. We'd prefer not to do so (or leverage it).
                sm.PostEvent ((uint)SmEvt.E.Success, "IMAPCONNSUC");
            }
            catch (ImapProtocolException e) {
                Log.Error (Log.LOG_IMAP, "Could not set up authenticated client: {0}", e);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPPROTOFAIL");
            }
            catch (AuthenticationException e) {
                Log.Error (Log.LOG_IMAP, "Authentication failed: {0}", e);
                sm.PostEvent ((uint)IMAP.ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTHFAIL");
            }
            catch (Exception e) {
                // FIXME - examine all possible exceptions.
                Log.Error (Log.LOG_IMAP, "Could not set up authenticated client: {0}", e);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPPROTOFAIL2");
            }
        }

        async public override void Cancel()
        {
                // FIXME system of timeout management and cancellation.
                cToken.Cancel ();
            await Client.DisconnectAsync (true).ConfigureAwait (false);
        }
    }
}
