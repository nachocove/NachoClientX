//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.Imap
{
    public class ImProtoControlApis
    {
        public partial class ImProtoControl : ProtoControl, IBEContext
        {
        }

        IProtoControlOwner Owner { set; get; }
        IProtoControl ProtoControl { set; get; }
        McProtocolState ProtocolState { get; set; }
        McServer Server { get; set; }
        McAccount Account { get; }
        McCred Cred { get; }
    }
}

