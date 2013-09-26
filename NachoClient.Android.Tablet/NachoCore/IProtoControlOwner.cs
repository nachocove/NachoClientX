using System;
using NachoCore.Utils;

namespace NachoCore
{
	public interface IProtoControlOwner
	{
		SQLiteConnectionWithEvents Db { set; get; }

		void CredReq (ProtoControl sender);
		void ServConfReq (ProtoControl sender);
		void HardFailInd (ProtoControl sender);
		void SoftFailInd (ProtoControl sender);
		bool RetryPermissionReq (ProtoControl sender, uint delaySeconds);
		void ServerOOSpaceInd (ProtoControl sender);
	}
}
