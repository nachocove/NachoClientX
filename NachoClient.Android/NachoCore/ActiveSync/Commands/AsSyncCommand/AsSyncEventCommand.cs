using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using System.IO;

namespace NachoCore.ActiveSync
{
    public partial class AsSyncCommand : AsCommand
    {
        // [MS-ASCMD]
        // When the client sends a Sync command request to the server and
        // a new item has been added to the server collection since the last
        // synchronization, the server responds with an Add element in a
        // Commands element. This Add element specifies the server ID and
        // data of the item to be added to the collection on the client.
        //
        // If the server ID in an Add element from the server matches the
        // server ID for an item on the client, the client treats the
        // addition as a CHANGE to the client item.
        //
        // This method is public for unit tests.
        public void ServerSaysAddCalendarItem (XElement command, McFolder folder)
        {
            Log.Info (Log.LOG_CALENDAR, "ServerSaysAddCalendarItem\n{0}", command.ToString ());
            ProcessCalendarItem (command, folder);
        }
        // [MS-ASCMD]
        // If a calendar:Exceptions node is not specified, the properties
        // for that calendar:Exceptions node will remain unchanged. If a
        // calendar:Exception node within the calendar:Exceptions node
        // is not present, that particular exception will remain unchanged.
        // If the airsyncbase:Body or airsyncbase:Data elements are not
        // present, the corresponding properties will remain unchanged.
        //
        // In all other cases, if an in-schema property is not specified
        // in a change request, the property is actively deleted from the
        // item on the server.
        //
        // This method is public for unit tests.
        public void ServerSaysChangeCalendarItem (XElement command, McFolder folder)
        {
            Log.Info (Log.LOG_CALENDAR, "ServerSaysChangeCalendarItem\n{0}", command.ToString ());
            ProcessCalendarItem (command, folder);
        }

        public void ProcessCalendarItem (XElement command, McFolder folder)
        {
            // Convert the event to an NcCalendar
            var h = new AsHelpers ();
            var r = h.ParseCalendar (m_ns, command, folder);
            McCalendar newItem = (McCalendar)r.GetObject ();

            System.Diagnostics.Trace.Assert (r.isOK ());
            System.Diagnostics.Trace.Assert (null != newItem);

            // Look up the event by ServerId
            McCalendar oldItem = null;

            try {
                oldItem = DataSource.Owner.Db.Get<McCalendar> (x => x.ServerId == newItem.ServerId);
            } catch (System.InvalidOperationException) {
                Log.Info (Log.LOG_CALENDAR, "ProcessCalendarItem: System.InvalidOperationException handled");
            } catch (Exception e) {
                Log.Info ("ProcessCalendarItem:\n{0}", e.ToString ());
            }

            // If there is no match, insert the new item.
            if (null == oldItem) {
                NcResult ir = DataSource.Owner.Db.Insert (newItem);
                System.Diagnostics.Trace.Assert (ir.isOK ());
                newItem.Id = ir.GetIndex ();
                MergeAttendees (newItem);
                MergeCategories (newItem);
                MergeExceptions (newItem);
                return;
            }

            // For a merge, we'll update the new entry following
            // the rules stated in the docs & repeated up above.

            // Pull over the Body
            if (0 == newItem.BodyId) {
                newItem.BodyId = newItem.BodyId;
            }

            // Overwrite the old item with the new item
            // to preserve the index, in
            newItem.Id = oldItem.Id;
            NcResult ur = DataSource.Owner.Db.Update (oldItem);
            System.Diagnostics.Trace.Assert (ur.isOK ());

            // Update the entries that refer to the updated entry
            MergeAttendees (newItem);
            MergeCategories (newItem);
            MergeExceptions (newItem);
            MergeRecurrences (newItem);
        }

        /// <param name="parentType">CALENDAR or EXCEPTION</param>
        /// <param name="parentId">Id field from McCalendar or NcException</param>
        public List<McAttendee> GetAttendees (McCalendarRoot r)
        {
            System.Diagnostics.Trace.Assert (r.Id > 0);
            string query = "select * from McAttendee where parentType = ? and parentId = ?";
            var l = DataSource.Owner.Db.Query<McAttendee> (query, McAttendee.GetParentType (r), r.Id);
            System.Diagnostics.Trace.Assert (l.Count >= 0);
            return l;
        }

        /// <param name="parentType">CALENDAR or EXCEPTION</param>
        /// <param name="parentId">Id field from McCalendar or NcException</param>
        public List<McCategory> GetCategories (McCalendarRoot r)
        {
            System.Diagnostics.Trace.Assert (r.Id > 0);
            string query = "select * from McCategory where parentType = ? and parentId = ?";
            var l = DataSource.Owner.Db.Query<McCategory> (query, McCategory.GetParentType (r), r.Id);
            System.Diagnostics.Trace.Assert (l.Count >= 0);
            return l;
        }

        /// <summary>
        /// Gets the exceptions.
        /// </summary>
        /// <returns>The exception list for this calendar item</returns>
        /// <param name="calendar">Calendar item</param>
        public List<McException> GetExceptions (McCalendar calendar)
        {
            System.Diagnostics.Trace.Assert (calendar.Id > 0);
            var l = DataSource.Owner.Db.Table<McException> ().Where (x => x.CalendarId == calendar.Id).ToList ();
            System.Diagnostics.Trace.Assert (l.Count >= 0);
            return l;
        }

        /// <summary>
        /// Gets the recurrences.
        /// </summary>
        /// <returns>The recurrences for this calendar item</returns>
        /// <param name="calendar">Calendar item</param>
        public List<McRecurrence> GetRecurrences (McCalendar calendar)
        {
            System.Diagnostics.Trace.Assert (calendar.Id > 0);
            var l = DataSource.Owner.Db.Table<McRecurrence> ().Where (x => x.CalendarId == calendar.Id).ToList ();
            System.Diagnostics.Trace.Assert (l.Count >= 0);
            return l;
        }

        /// <summary>
        /// I didn't see any fancy rules about how to merge
        /// attendee lists, so taking the slow & safe road
        /// of deleting the old and inserting the new.
        /// </summary>
        // TODO: Handle errors
        public void MergeAttendees (McCalendarRoot c)
        {
            // Get the old list
            System.Diagnostics.Trace.Assert (null != c);
            List<McAttendee> attendees = GetAttendees (c);

            // Delete the old
            foreach (var attendee in attendees) {
                DataSource.Owner.Db.Delete (attendee);
            }

            // Add the new, if any
            System.Diagnostics.Trace.Assert (null != c.attendees);

            // Add the new
            foreach (var attendee in c.attendees) {
                if (attendee.Id > 0) {
                    NcResult r = DataSource.Owner.Db.Update (attendee);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                } else {
                    attendee.ParentId = c.Id;
                    attendee.ParentType = McAttendee.GetParentType (c);
                    NcResult r = DataSource.Owner.Db.Insert (attendee);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                    attendee.Id = r.GetIndex ();
                }
            }

        }

        /// <summary>
        /// I didn't see any fancy rules about how to merge
        /// category lists, so taking the slow & safe road
        /// of deleting the old and inserting the new.
        /// </summary>
        /// <param name="c">C.</param>
        // TODO: Handle errors
        public void MergeCategories (McCalendarRoot c)
        {
            // Get the old list
            System.Diagnostics.Trace.Assert (null != c);
            List<McCategory> categories = GetCategories (c);

            // Delete the old
            foreach (var category in categories) {
                DataSource.Owner.Db.Delete (category);
            }

            // Add the new, if any
            System.Diagnostics.Trace.Assert (null != c.categories);

            // Add the new
            foreach (var category in c.categories) {
                if (category.Id > 0) {
                    NcResult r = DataSource.Owner.Db.Update (category);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                } else {
                    category.ParentId = McCategory.GetParentType (c);
                    NcResult r = DataSource.Owner.Db.Insert (category);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                    category.Id = r.GetIndex ();
                }
            }
        }

        /// <summary>
        /// I didn't see any fancy rules about how to merge
        /// exception lists, so taking the slow & safe road
        /// of deleting the old and inserting the new.
        /// </summary>
        /// <param name="c">C.</param>
        // TODO: Handle errors
        public void MergeExceptions (McCalendar c)
        {
            // Get the old list
            System.Diagnostics.Trace.Assert (null != c);
            List<McException> exceptions = GetExceptions (c);

            // Delete the old
            foreach (var exception in exceptions) {
                DataSource.Owner.Db.Delete (exception);
            }

            // Add the new, if any
            System.Diagnostics.Trace.Assert (null != c.exceptions);

            // Add the new
            foreach (var exception in c.exceptions) {
                if (exception.Id > 0) {
                    NcResult r = DataSource.Owner.Db.Update (exception);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                } else {
                    NcResult r = DataSource.Owner.Db.Insert (exception);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                    exception.Id = r.GetIndex ();
                }
                MergeAttendees (exception);
                MergeCategories (exception);
            }
        }

        public void MergeRecurrences (McCalendar c)
        {
            // Get the old list
            System.Diagnostics.Trace.Assert (null != c);
            List<McRecurrence> recurrences = GetRecurrences (c);

            // Delete the old
            foreach (var recurrence in recurrences) {
                DataSource.Owner.Db.Delete (recurrence);
            }

            // Add the new, if any
            System.Diagnostics.Trace.Assert (null != c.recurrences);

            // Add the new
            foreach (var recurrence in c.recurrences) {
                if (recurrence.Id > 0) {
                    NcResult r = DataSource.Owner.Db.Update (recurrence);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                } else {
                    NcResult r = DataSource.Owner.Db.Insert (recurrence);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                    recurrence.Id = r.GetIndex ();
                }
            }
        }

        /// <summary>
        /// Deletes the exception and its attendees and categories
        /// </summary>
        /// <param name="exception">An NcException object</param>
        // TODO: error checking and unit tests.
        public void DeleteException(McException exception)
        {
            System.Diagnostics.Trace.Assert (null != exception);

            var attendees = GetAttendees (exception);
            System.Diagnostics.Trace.Assert (null != attendees);

            foreach (var attendee in attendees) {
                DataSource.Owner.Db.Delete (attendee);
            }

            var categories = GetCategories (exception);
            System.Diagnostics.Trace.Assert (null != categories);

            foreach (var category in categories) {
                DataSource.Owner.Db.Delete (category);
            }

            DataSource.Owner.Db.Delete (exception);

        }
    }
}
   
