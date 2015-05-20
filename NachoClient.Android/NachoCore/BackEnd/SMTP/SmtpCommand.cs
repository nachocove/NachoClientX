//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.SMTP
{
    public abstract class SmtpCommand : ISmtpCommand
    {
        public void Execute (NcStateMachine sm)
        {
        }

        public void Cancel ()
        {
        }
    }
}

