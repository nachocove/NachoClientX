//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.IMAP
{
    public class ImapProtoControl : NcProtoControl, IPushAssistOwner
    {
        public ImapProtoControl (IProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            Capabilities = McAccount.ImapCapabilities;
            SetupAccount ();
        }

        public PushAssistParameters PushAssistParameters ()
        {
            NcAssert.True (false);
            return null;
        }
    }
}

