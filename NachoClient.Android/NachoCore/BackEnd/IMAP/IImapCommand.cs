//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.IMAP
{
    interface IImapCommand
    {
        void Execute (NcStateMachine sm);
        void Cancel ();
    }
}
