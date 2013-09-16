using System;
using System.Linq.Expressions;
using SQLite;

namespace NachoCore.Utils
{
	public class SQLiteConnectionWithEvents
	{
		private SQLiteConnection m_db;

		public SQLiteConnectionWithEvents (string databasePath, bool storeDateTimeAsTicks = false)
			: this (databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create, storeDateTimeAsTicks) { 
		}

		public SQLiteConnectionWithEvents (string databasePath, SQLiteOpenFlags openFlags, bool storeDateTimeAsTicks = false) {
			m_db = new SQLiteConnection (databasePath, openFlags, storeDateTimeAsTicks);
		}
		public int CreateTable<T>(CreateFlags createFlags = CreateFlags.None) {
			return m_db.CreateTable<T> (createFlags);
		}
		public int CreateTable(Type ty, CreateFlags createFlags = CreateFlags.None) {
			return m_db.CreateTable (ty, createFlags);
		}
		public int InsertAll (System.Collections.IEnumerable objects) {
			return m_db.Insert (objects);
		}
		public int InsertAll (System.Collections.IEnumerable objects, string extra) {
			return m_db.InsertAll (objects, extra);
		}
		public int InsertAll (System.Collections.IEnumerable objects, Type objType) {
			return m_db.InsertAll (objects, objType);
		}
		public int Insert (object obj) {
			return m_db.Insert (obj);
		}
		public int InsertOrReplace (object obj) {
			return m_db.InsertOrReplace (obj);
		}
		public int Insert (object obj, Type objType) {
			return m_db.Insert (obj, objType);
		}
		public int InsertOrReplace (object obj, Type objType) {
			return m_db.InsertOrReplace (obj, objType);
		}
		public int Insert (object obj, string extra) {
			return m_db.Insert (obj, extra);
		}
		public int Insert (object obj, string extra, Type objType) {
			return m_db.Insert (obj, extra, objType);
		}
		public int Delete (object objectToDelete) {
			return m_db.Delete (objectToDelete);
		}
		public int Delete<T> (object primaryKey) {
			return m_db.Delete<T> (primaryKey);
		}
		public int DeleteAll<T> () {
			return m_db.DeleteAll<T> ();
		}
		public int Update (object obj) {
			return m_db.Update (obj);
		}
		public int Update (object obj, Type objType) {
			return m_db.Update (obj, objType);
		}
		public int UpdateAll (System.Collections.IEnumerable objects) {
			return m_db.UpdateAll (objects);
		}
		public TableQuery<T> Table<T> () where T : new() {
			return m_db.Table<T>();
		}
		public T Find<T> (object pk) where T : new () {
			return m_db.Find<T> (pk);
		}
		public T Get<T> (Expression<Func<T, bool>> predicate) where T : new() {
			return m_db.Get<T> (predicate);
		}
	}
}

