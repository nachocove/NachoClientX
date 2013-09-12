using System;

namespace NachoCore.ActiveSync
{
	public interface IAsOwner
	{
		void CredRequest (AsControl sender);
		void ServConfRequest (AsControl sender);
		void HardFailureIndication (AsControl sender);
	}
}
