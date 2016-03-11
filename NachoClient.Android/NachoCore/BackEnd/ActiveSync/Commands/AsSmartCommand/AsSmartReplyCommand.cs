//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsSmartReplyCommand : AsSmartCommand
    {
        public AsSmartReplyCommand (IBEContext dataSource, McPending pending) : base (dataSource)
        {
            CommandName = Xml.ComposeMail.SmartReply;
            PendingSingle = pending;
            PendingSingle.MarkDispatched ();
            EmailMessage = McAbstrObject.QueryById<McEmailMessage> (PendingSingle.ItemId);
        }
    }
}

