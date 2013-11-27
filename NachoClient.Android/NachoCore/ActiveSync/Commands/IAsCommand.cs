//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public interface IAsCommand
    {
        void Execute (StateMachine sm);
        void Cancel ();
    }
}

