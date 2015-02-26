//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.Model
{
    /// <summary>
    /// List of exceptions associated with the calendar entry
    /// </summary>
    public partial class McException : McAbstrCalendarRoot
    {
        [Indexed]
        public Int64 CalendarId { get; set; }

        /// Has this exception been deleted?  Exception only.
        public uint Deleted { get; set; }

        /// Start time of the original recurring meeting (Compact DateTime). Exception only.
        public DateTime ExceptionStartTime { get; set; }

        public static List<McException> QueryForExceptionId (int calendarId, DateTime exceptionStartTime)
        {
            var query = "SELECT * from McException WHERE CalendarId = ? AND ExceptionStartTime = ?";
            var result = NcModel.Instance.Db.Query<McException> (query, calendarId, exceptionStartTime).ToList ();
            return result;
        }

        /// <summary>
        /// All of the exceptions associated with the given calendar item.
        /// </summary>
        public static IEnumerable<McException> QueryExceptionsForCalendarItem (int calendarId)
        {
            return NcModel.Instance.Db.Table<McException> ().Where (x => x.CalendarId == calendarId);
        }
    }
}
