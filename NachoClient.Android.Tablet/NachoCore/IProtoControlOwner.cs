using System;
using NachoCore.Utils;

namespace NachoCore
{
	public interface IProtoControlOwner
	{
		SQLiteConnectionWithEvents Db { set; get; }

		void CredRequest (ProtoControl sender);
		void ServConfRequest (ProtoControl sender);
		void HardFailureIndication (ProtoControl sender);
		void SoftFailureIndication (ProtoControl sender);
	}
}
