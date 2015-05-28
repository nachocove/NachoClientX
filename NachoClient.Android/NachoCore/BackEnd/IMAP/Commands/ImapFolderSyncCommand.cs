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
    public class ImapFolderSyncCommand : ImapCommand
    {
        public ImapFolderSyncCommand (IBEContext beContext, ImapClient imap) : base (beContext, imap)
        {
        }

        public override void Execute (NcStateMachine sm)
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
                    Log.Error (Log.LOG_IMAP, "Could not find special folder {0}: {1}", special.ToString (), ex);
                }
            }
            sm.PostEvent ((uint)SmEvt.E.Success, "IMAPFSYNCSUC");
        }
    }
}
