using System;

namespace NachoCore.Utils
{
	public delegate void SQLiteEventHandler (Type klass, int Id, EventArgs e);
	public interface ISQLiteEventable
	{
		int Id { get; set;}
		void Fire_DidWriteToDb (Type klass, int id, EventArgs e);
		void Fire_WillDeleteFromDb (Type klass, int id, EventArgs e);
	}
}

