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
        public static void ServerSaysAddCalendarItem (XElement command, McFolder folder)
        {
            ProcessCalendarItem (command, folder, true);
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
        public static void ServerSaysChangeCalendarItem (XElement command, McFolder folder)
        {
            ProcessCalendarItem (command, folder, false);
        }

        public static void ProcessCalendarItem (XElement command, McFolder folder, bool isAdd)
        {
            // Convert the event to an NcCalendar
            var h = new AsHelpers ();
            var r = h.ParseCalendar (Ns, command);
            McCalendar newItem = r.GetValue<McCalendar> ();

            NcAssert.True (r.isOK (), "ParseCalendar");
            NcAssert.NotNull (newItem, "newItem");

            // Look up the event by ServerId
            McCalendar oldItem = null;

            if (isAdd) {
                oldItem = McAbstrItem.QueryByClientId<McCalendar> (folder.AccountId, newItem.ClientId);
            } else {
                oldItem = McAbstrFolderEntry.QueryByServerId<McCalendar> (folder.AccountId, newItem.ServerId);
            }

            // If there is no match, insert the new item.
            if (null == oldItem) {
                newItem.AccountId = folder.AccountId;
                int ir = newItem.Insert ();
                NcAssert.True (0 < ir, "newItem.Insert");
                folder.Link (newItem);
                return;
            }

            // For a merge, we'll update the new entry following
            // the rules stated in the docs & repeated up above.

            // Pull over the Body
            if (0 == newItem.BodyId) {
                newItem.BodyId = oldItem.BodyId;
            }

            // Overwrite the old item with the new item
            // to preserve the index, in
            newItem.Id = oldItem.Id;
            int ur = oldItem.Update ();
            NcAssert.True (0 < ur, "oldItem.Update");

            // Update the entries that refer to the updated entry
            MergeAttendees (newItem);
            MergeCategories (newItem);
            MergeExceptions (newItem);
            MergeRecurrences (newItem);
        }

        /// <param name="parentType">CALENDAR or EXCEPTION</param>
        /// <param name="parentId">Id field from McCalendar or NcException</param>
        /// FIXME - move to McAttendee.
        public static List<McAttendee> GetAttendees (McAbstrCalendarRoot r)
        {
            NcAssert.True (r.Id > 0, "r.Id > 0");
            string query = "select * from McAttendee where parentType = ? and parentId = ?";
            var l = NcModel.Instance.Db.Query<McAttendee> (query, McAttendee.GetParentType (r), r.Id);
            NcAssert.True (l.Count >= 0, "l.Count >= 0");
            return l;
        }

        /// <param name="parentType">CALENDAR or EXCEPTION</param>
        /// <param name="parentId">Id field from McCalendar or NcException</param>
        public static List<McCalendarCategory> GetCategories (McAbstrCalendarRoot r)
        {
            NcAssert.True (r.Id > 0, "r.Id > 0");
            string query = "select * from McCalendarCategory where parentType = ? and parentId = ?";
            var l = NcModel.Instance.Db.Query<McCalendarCategory> (query, McCalendarCategory.GetParentType (r), r.Id);
            NcAssert.True (l.Count >= 0, "l.Count >= 0");
            return l;
        }

        /// <summary>
        /// Gets the exceptions.
        /// </summary>
        /// <returns>The exception list for this calendar item</returns>
        /// <param name="calendar">Calendar item</param>
        public static List<McException> GetExceptions (McCalendar calendar)
        {
            NcAssert.True (calendar.Id > 0, "calendar.Id > 0");
            var l = NcModel.Instance.Db.Table<McException> ().Where (x => x.CalendarId == calendar.Id).ToList ();
            NcAssert.True (l.Count >= 0, "l.Count >= 0");
            return l;
        }

        /// <summary>
        /// Gets the recurrences.
        /// </summary>
        /// <returns>The recurrences for this calendar item</returns>
        /// <param name="calendar">Calendar item</param>
        public static List<McRecurrence> GetRecurrences (McCalendar calendar)
        {
            NcAssert.True (calendar.Id > 0, "calendar.Id > 0");
            var l = NcModel.Instance.Db.Table<McRecurrence> ().Where (x => x.CalendarId == calendar.Id).ToList ();
            NcAssert.True (l.Count >= 0, "l.Count >= 0");
            return l;
        }

        /// <summary>
        /// I didn't see any fancy rules about how to merge
        /// attendee lists, so taking the slow & safe road
        /// of deleting the old and inserting the new.
        /// </summary>
        // TODO: Handle errors
        public static void MergeAttendees (McAbstrCalendarRoot c)
        {
            // Get the old list
            NcAssert.NotNull (c, "McCalendarRoot c");
            List<McAttendee> attendees = GetAttendees (c);

            // FIXME: use update instead of delete & add

            // Delete the old
            foreach (var attendee in attendees) {
                attendee.Delete ();
            }

            // Add the new, if any
            NcAssert.NotNull (c.attendees, "c.attendees");

            // Add the new
            foreach (var attendee in c.attendees) {
                attendee.ParentId = c.Id;
                attendee.ParentType = McAttendee.GetParentType (c);
                int r = attendee.Insert ();
                NcAssert.True (0 < r, "attendee.Insert");
            }

        }

        /// <summary>
        /// I didn't see any fancy rules about how to merge
        /// category lists, so taking the slow & safe road
        /// of deleting the old and inserting the new.
        /// </summary>
        /// <param name="c">C.</param>
        // TODO: Handle errors
        public static void MergeCategories (McAbstrCalendarRoot c)
        {
            // Get the old list
            NcAssert.NotNull (c, "McCalendarRoot c");
            List<McCalendarCategory> categories = GetCategories (c);

            // Delete the old
            foreach (var category in categories) {
                category.Delete ();
            }

            // Add the new, if any
            NcAssert.NotNull (c.categories, "c.categories");

            // Add the new
            foreach (var category in c.categories) {
                if (category.Id > 0) {
                    int r = category.Update ();
                    NcAssert.True (0 < r, "category.Update");
                } else {
                    category.ParentId = McCalendarCategory.GetParentType (c);
                    int r = category.Insert ();
                    NcAssert.True (0 < r, "category.Insert");
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
        public static void MergeExceptions (McCalendar c)
        {
            // Get the old list
            NcAssert.True (null != c);
            List<McException> exceptions = GetExceptions (c);

            // Delete the old
            foreach (var exception in exceptions) {
                exception.Delete ();
            }

            // Add the new, if any
            NcAssert.NotNull (c.exceptions, "c.exceptions");

            // Add the new
            foreach (var exception in c.exceptions) {
                exception.AccountId = c.AccountId;
                if (exception.Id > 0) {
                    int r = exception.Update ();
                    NcAssert.True (0 < r, "exception.Update");
                } else {
                    int r = exception.Insert ();
                    NcAssert.True (0 < r, "exception.Insert");
                }
                MergeAttendees (exception);
                MergeCategories (exception);
            }
        }

        public static void MergeRecurrences (McCalendar c)
        {
            // Get the old list
            NcAssert.True (null != c);
            List<McRecurrence> recurrences = GetRecurrences (c);

            // Delete the old
            foreach (var recurrence in recurrences) {
                recurrence.Delete ();
            }

            // Add the new, if any
            NcAssert.NotNull (c.recurrences, "c.recurrences");

            // Add the new
            foreach (var recurrence in c.recurrences) {
                if (recurrence.Id > 0) {
                    int r = recurrence.Update ();
                    NcAssert.True (0 < r, "recurrence.Update");
                } else {
                    int r = recurrence.Insert ();
                    NcAssert.True (0 < r, "ecurrence.Insert");
                }
            }
        }

        /// <summary>
        /// Deletes the exception and its attendees and categories
        /// </summary>
        /// <param name="exception">An NcException object</param>
        // TODO: error checking and unit tests.
        public void DeleteException (McException exception)
        {
            NcAssert.True (null != exception);

            var attendees = GetAttendees (exception);
            NcAssert.True (null != attendees);

            foreach (var attendee in attendees) {
                attendee.Delete ();
            }

            var categories = GetCategories (exception);
            NcAssert.True (null != categories);

            foreach (var category in categories) {
                category.Delete ();
            }

            exception.Delete ();
        }
    }
}
   
