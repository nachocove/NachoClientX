// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public interface IAsOperation
    {
        void Execute (NcStateMachine sm);
        void Cancel ();
    }
}

