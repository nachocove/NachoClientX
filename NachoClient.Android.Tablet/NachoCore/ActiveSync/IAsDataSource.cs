using System;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public interface IAsDataSource
	{
		SQLiteConnectionWithEvents Db { set; get; }
		NcProtocolState ProtocolState { get; set;}
		NcServer Server { get; set;}
		NcAccount Account { get; set;}
		NcCred Cred { get; set;}
	}
}

