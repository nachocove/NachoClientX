//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsSmartReplyCommand : AsSmartCommand
    {
        public AsSmartReplyCommand (IBEContext dataSource) : base (dataSource)
        {
            CommandName = Xml.ComposeMail.SmartReply;
            PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.EmailReply);
            PendingSingle.MarkDispached ();
            EmailMessage = McAbstrObject.QueryById<McEmailMessage> (PendingSingle.ItemId);
        }
    }
}

