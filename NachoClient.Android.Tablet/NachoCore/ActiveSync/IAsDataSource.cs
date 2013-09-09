using System;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
	public interface IAsDataSource
	{
		NcProtocolState ProtocolState { get; set;}
		NcServer Server { get; set;}
		NcAccount Account { get; set;}
		NcCred Cred { get; set;}
	}
}

