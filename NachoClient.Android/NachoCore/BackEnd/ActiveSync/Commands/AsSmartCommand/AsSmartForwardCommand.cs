//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsSmartForwardCommand : AsSmartCommand
    {
        public AsSmartForwardCommand (IAsDataSource dataSource) : base (dataSource)
        {
            CommandName = Xml.ComposeMail.SmartForward;
            Update = NextPending (McPending.Operations.EmailForward);
            EmailMessage = McObject.QueryById<McEmailMessage> (Update.EmailMessageId);
        }
    }
}

