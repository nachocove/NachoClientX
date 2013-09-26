using System;
using NachoCore.Model; // FIXME - decouple from Model.

namespace NachoCore.Utils
{
	public delegate void SQLiteEventHandler (BackEnd.Actors actor, NcEventable target, EventArgs e);
	public interface ISQLiteEventable
	{
		int Id { get; set;}
		int AccountId { get; set; }
		void Fire_DidWriteToDb (BackEnd.Actors actor, NcEventable target, EventArgs e);
		void Fire_WillDeleteFromDb (BackEnd.Actors actor, NcEventable target, EventArgs e);
	}
}

