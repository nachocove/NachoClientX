using SQLite;
using System;
using NachoCore.Utils;

namespace NachoCore.Model
{
	public abstract class NcEventable : NcObject, ISQLiteEventable
	{
		public static event SQLiteEventHandler DidWriteToDb;
		public static event SQLiteEventHandler WillDeleteFromDb;

		[Indexed]
		public int AccountId { get; set; }

		public void Fire_DidWriteToDb (BackEnd.Actors actor, NcEventable target, EventArgs e) {
			if (null != DidWriteToDb) {
				DidWriteToDb (actor, target, e);
			}
		}
		public void Fire_WillDeleteFromDb (BackEnd.Actors actor, NcEventable target, EventArgs e) {
			if (null != WillDeleteFromDb) {
				WillDeleteFromDb (actor, target, e);
			}
		}
	}
}

