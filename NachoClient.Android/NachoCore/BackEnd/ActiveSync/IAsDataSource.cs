using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public interface IAsDataSource
    {
        IProtoControlOwner Owner { set; get; }
        AsProtoControl Control { set; get; }
        McProtocolState ProtocolState { get; set; }
        McServer Server { get; set; }
        McAccount Account { get; }
        McCred Cred { get; }
    }
}

