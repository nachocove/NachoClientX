using System;
using NachoCore.Utils;

namespace NachoCore.Model
{
	public class NcEventable : NcObject, ISQLiteEventable
	{
		public static event SQLiteEventHandler DidWriteToDb;
		public static event SQLiteEventHandler WillDeleteFromDb;
		public void Fire_DidWriteToDb (Type klass, int id, EventArgs e) {
			if (null != DidWriteToDb) {
				DidWriteToDb (klass, id, e);
			}
		}
		public void Fire_WillDeleteFromDb (Type klass, int id, EventArgs e) {
			if (null != WillDeleteFromDb) {
				WillDeleteFromDb (klass, id, e);
			}
		}
	}
}

