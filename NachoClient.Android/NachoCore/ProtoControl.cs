using System;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
	public abstract class ProtoControl
	{
		public NcAccount Account { set; get; }
		public StateMachine Sm { set; get; }

        public abstract void Execute ();
        public abstract void CertAskResp (bool isOkay);
        public abstract void ServerConfResp ();
        public abstract void CredResp ();
	}
}
