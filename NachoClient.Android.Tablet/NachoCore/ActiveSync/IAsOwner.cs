using System;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public interface IAsOwner
	{
		SQLiteConnectionWithEvents Db { set; get; }

		void CredRequest (AsControl sender);
		void ServConfRequest (AsControl sender);
		void HardFailureIndication (AsControl sender);
	}
}
