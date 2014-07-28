using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public interface IBEContext
    {
        IProtoControlOwner Owner { set; get; }
        // FIXME - this is AS-specific.
        AsProtoControl ProtoControl { set; get; }
        McProtocolState ProtocolState { get; set; }
        McServer Server { get; set; }
        McAccount Account { get; }
        McCred Cred { get; }
    }
}

