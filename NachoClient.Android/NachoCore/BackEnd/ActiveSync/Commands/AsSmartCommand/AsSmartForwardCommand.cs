//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsSmartForwardCommand : AsSmartCommand
    {
        public AsSmartForwardCommand (IBEContext dataSource, McPending pending) : base (dataSource)
        {
            CommandName = Xml.ComposeMail.SmartForward;
            PendingSingle = pending;
            PendingSingle.MarkDispatched ();
            EmailMessage = McAbstrObject.QueryById<McEmailMessage> (PendingSingle.ItemId);
        }
    }
}

