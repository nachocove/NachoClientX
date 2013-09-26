using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public class StagedChange
	{
		public NcPendingUpdate Update;
		public bool IsDispatched;
	}

	public class StagedChanges
	{
		public Dictionary<int,List<StagedChange>> EmailMessageDeletes;
	}

	public interface IAsDataSource
	{
		IProtoControlOwner Owner { set; get; }
		AsProtoControl Control { set; get; }
		NcProtocolState ProtocolState { get; set; }
		NcServer Server { get; set; }
		NcAccount Account { get; set; }
		NcCred Cred { get; set; }
		StagedChanges Staged { get; set; } 
	}
}

