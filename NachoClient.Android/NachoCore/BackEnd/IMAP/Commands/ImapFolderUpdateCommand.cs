//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.IMAP
{
    public class ImapFolderUpdateCommand : ImapCommand
    {
        public ImapFolderUpdateCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            // TODO - app does not use this yet.
        }
    }
}

