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
        NcProtocolState ProtocolState { get; set; }
		NcServer Server { get; set; }
        NcAccount Account { get; }
		NcCred Cred { get; }
	}
}

