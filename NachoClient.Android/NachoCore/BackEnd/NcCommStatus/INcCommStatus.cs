﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoPlatform;

namespace NachoCore.Utils
{
    public delegate void NcCommStatusServerEventHandler (Object sender, NcCommStatusServerEventArgs e);
    public interface INcCommStatus
    {
        event NcCommStatusServerEventHandler CommStatusServerEvent;
        event NetStatusEventHandler CommStatusNetEvent;

        void ReportCommResult (int serverId, bool didFailGenerally);

        void ReportCommResult (string host, bool didFailGenerally);

        void Reset (int serverId);

        void Refresh ();
    }
}

