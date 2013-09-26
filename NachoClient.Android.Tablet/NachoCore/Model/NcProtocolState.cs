using System;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
	// NOTE: eventually this will be a base class, with an active-sync sub-class.
	public class NcProtocolState : NcObject
	{
		public NcProtocolState() {
			AsProtocolVersion = "12.0";
			AsPolicyKey = "0";
			AsSyncKey = "0";
			State = (uint)St.Start;
		}
		public string AsProtocolVersion { get; set;}
		public string AsPolicyKey { get; set;}
		public string AsSyncKey { get; set;}
		public uint State { get; set;}
	}
}

