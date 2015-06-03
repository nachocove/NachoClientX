//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.IMAP
{
    public class ImapWaitCommand : ImapCommand
    {
        public ImapWaitCommand (IBEContext beContext, int duration, bool earlyOnECChange) : base (beContext)
        {
            // FIXME.
        }
    }
}

