//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.IMAP
{
    public class ImapEmailDeleteCommand : ImapCommand
    {
        public ImapEmailDeleteCommand (IBEContext beContext, McPending pending) : base (beContext)
        {
            // FIXME
        }
    }
}

