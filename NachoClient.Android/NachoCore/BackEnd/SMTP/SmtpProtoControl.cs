//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.SMTP
{
    public class SmtpProtoControl : NcProtoControl
    {
        public SmtpProtoControl (IProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = McAccount.SmtpCapabilities;
        }
    }
}

