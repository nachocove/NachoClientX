using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.ActiveSync; // TODO - remove this reference.

namespace NachoCore
{
    public interface IBEContext
    {
        IProtoControlOwner Owner { set; get; }
        // TODO - this is AS-specific.
        AsProtoControl ProtoControl { set; get; }
        McProtocolState ProtocolState { get; set; }
        McServer Server { get; set; }
        McAccount Account { get; }
        McCred Cred { get; }
    }
}

