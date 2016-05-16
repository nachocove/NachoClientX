using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public interface IBEContext
    {
        INcProtoControlOwner Owner { set; get; }
        NcProtoControl ProtoControl { set; get; }
        McProtocolState ProtocolState { get; }
        McServer Server { get; set; }
        McAccount Account { get; }
        McCred Cred { get; }
    }
}

