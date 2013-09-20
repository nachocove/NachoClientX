using System;

namespace NachoCore.Utils
{
	public delegate void SQLiteEventHandler (BackEnd.Actors actor, int accountId, Type klass, int id, EventArgs e);
	public interface ISQLiteEventable
	{
		int Id { get; set;}
		int AccountId { get; set; }
		void Fire_DidWriteToDb (BackEnd.Actors actor, int accountId, Type klass, int id, EventArgs e);
		void Fire_WillDeleteFromDb (BackEnd.Actors actor, int accountId, Type klass, int id, EventArgs e);
	}
}

