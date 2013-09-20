using SQLite;
using System;
using NachoCore.Utils;

namespace NachoCore.Model
{
	public class NcEventable : NcObject, ISQLiteEventable
	{
		public static event SQLiteEventHandler DidWriteToDb;
		public static event SQLiteEventHandler WillDeleteFromDb;

		[Indexed]
		public int AccountId { get; set; } // Don't-Care for NcAccount records.

		public void Fire_DidWriteToDb (BackEnd.Actors actor,
		                               int accountId, Type klass, int id, EventArgs e) {
			if (null != DidWriteToDb) {
				DidWriteToDb (actor, accountId, klass, id, e);
			}
		}
		public void Fire_WillDeleteFromDb (BackEnd.Actors actor,
		                                   int accountId, Type klass, int id, EventArgs e) {
			if (null != WillDeleteFromDb) {
				WillDeleteFromDb (actor, accountId, klass, id, e);
			}
		}
	}
}

