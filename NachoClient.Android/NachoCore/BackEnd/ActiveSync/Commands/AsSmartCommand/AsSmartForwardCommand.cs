//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsSmartForwardCommand : AsSmartCommand
    {
        public AsSmartForwardCommand (IBEContext dataSource) : base (dataSource)
        {
            CommandName = Xml.ComposeMail.SmartForward;
            PendingSingle = McPending.QueryFirstByOperation (BEContext.Account.Id, McPending.Operations.EmailForward);
            PendingSingle.MarkDispached ();
            EmailMessage = McObject.QueryById<McEmailMessage> (PendingSingle.EmailMessageId);
        }
    }
}

