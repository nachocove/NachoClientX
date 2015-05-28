//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
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
    public class ImapCommand : IImapCommand
    {
        protected IBEContext BEContext;
        protected ImapClient Client { get; set; }
        public CancellationTokenSource Cts { get; protected set; }

        public ImapCommand (IBEContext beContext, ImapClient imap)
        {
            Cts = new CancellationTokenSource ();
            BEContext = beContext;
            Client = imap;
        }

        public virtual void Execute (NcStateMachine sm)
        {
        }

        public virtual void Cancel ()
        {
            Cts.Cancel ();
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
}
