//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public enum PickActionEnum { Sync, Ping, QOop, Fetch, Wait };
    public interface IAsStrategy
    {
        Tuple<uint, List<Tuple<McFolder, List<McPending>>>> SyncKit (bool cantBeEmpty);
        Tuple<PickActionEnum, AsCommand> Pick ();
    }
}

