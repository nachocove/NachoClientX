//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsSmartReplyCommand : AsSmartCommand
    {
        public AsSmartReplyCommand (IAsDataSource dataSource) : base (dataSource)
        {
            CommandName = Xml.ComposeMail.SmartReply;
            Update = NextPending (McPending.Operations.EmailReply);
            EmailMessage = McEmailMessage.QueryById (Update.EmailMessageId);
        }
    }
}

