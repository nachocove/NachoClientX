//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Model;
using MailKit.Net.Imap;

namespace NachoCore.IMAP
{
    public class ImapFetchAttachmentCommand : ImapCommand
    {
        public ImapFetchAttachmentCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            // FIXME
        }
    }
}

