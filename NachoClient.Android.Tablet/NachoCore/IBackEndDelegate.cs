using System;
using NachoCore.Model;

namespace NachoCore
{
	// "Delegate" in the Cocoa sense of the word.
	public interface IBackEndDelegate
	{
		/* CredRequest: When called, the callee must gather the credential for the specified 
		 * account and add/update it to/in the DB. The callee must then update
		 * the account record. The BE will act based on the update event for the
		 * account record.
		 */
		void CredRequest (NcAccount account);
		/* ServConfRequest: When called the callee must gather the server information for the 
		 * specified account and nd add/update it to/in the DB. The callee must then update
		 * the account record. The BE will act based on the update event for the
		 * account record.
		 */
		void ServConfRequest (NcAccount account);
		/* HardFailureIndication: Called to indicate to the callee that there is a failure
		 * that will require some sort of intervention. The callee must call the BE method
		 * Start(account) to get the BE going again (post intervention).
		 */
		void HardFailureIndication (NcAccount account);
		/* SoftFailureIndication: Called to indicate that "it aint workin' right now." The
		 * callee must call the BE method Start(account) to get the BE going again. We will
		 * want to add some autorecovery here in the future.
		 */
		void SoftFailureIndication (NcAccount account);
	}
}

