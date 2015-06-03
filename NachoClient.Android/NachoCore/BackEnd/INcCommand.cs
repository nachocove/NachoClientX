//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore
{
    public interface INcCommand
    {
        void Execute (NcStateMachine sm);
        void Cancel ();
    }
}

