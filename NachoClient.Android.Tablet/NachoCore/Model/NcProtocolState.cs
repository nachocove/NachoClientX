using System;

namespace NachoCore.Model
{
	public class NcProtocolState
	{
		public string AsProtocolVersion { get; set;}
		public string AsPolicyKey { get; set;}
		public NcProtocolState() {
			AsPolicyKey = "0";
		}
	}
}

