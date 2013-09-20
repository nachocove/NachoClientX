using System;

namespace NachoCore.Model
{
	public class NcPendingUpdate : NcObject
	{
		public enum Operations {CreateUpdate=0, Delete};

		public Operations Operation { set; get;}
		public int AccountId { set; get;}
		public int TargetId { set; get;}
		public Type Klass { set; get;}
	}
}

