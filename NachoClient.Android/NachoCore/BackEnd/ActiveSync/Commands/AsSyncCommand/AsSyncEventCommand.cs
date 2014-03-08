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
            Log.Info (Log.LOG_CALENDAR, "ServerSaysAddCalendarItem\n{0}", command);
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
        public void ServerSaysChangeCalendarItem (XElement command, McFolder folder)
        {
            Log.Info (Log.LOG_CALENDAR, "ServerSaysChangeCalendarItem\n{0}", command);
            ProcessCalendarItem (command, folder, false);
        }

        public void ProcessCalendarItem (XElement command, McFolder folder, bool isAdd)
        {
            // Convert the event to an NcCalendar
            var h = new AsHelpers ();
            var r = h.ParseCalendar (m_ns, command);
            McCalendar newItem = r.GetValue<McCalendar> ();

            NachoCore.NachoAssert.True (r.isOK ());
            NachoCore.NachoAssert.True (null != newItem);

            // Look up the event by ServerId
            McCalendar oldItem = null;

            if (isAdd) {
                oldItem = McItem.QueryByClientId<McCalendar> (folder.AccountId, newItem.ClientId);
            } else {
                oldItem = McFolderEntry.QueryByServerId<McCalendar> (folder.AccountId, newItem.ServerId);
            }

            // If there is no match, insert the new item.
            if (null == oldItem) {
                newItem.AccountId = folder.AccountId;
                int ir = newItem.Insert ();
                NachoCore.NachoAssert.True (0 < ir);
                MergeAttendees (newItem);
                MergeCategories (newItem);
                MergeExceptions (newItem);
                var map = new McMapFolderItem (newItem.AccountId) {
                    FolderId = folder.Id,
                    ItemId = newItem.Id,
                    ClassCode = (uint)McItem.ClassCodeEnum.Calendar,
                };
                map.Insert ();
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
            NachoCore.NachoAssert.True (0 < ur);

            // Update the entries that refer to the updated entry
            MergeAttendees (newItem);
            MergeCategories (newItem);
            MergeExceptions (newItem);
            MergeRecurrences (newItem);
        }

        /// <param name="parentType">CALENDAR or EXCEPTION</param>
        /// <param name="parentId">Id field from McCalendar or NcException</param>
        /// FIXME - move to McAttendee.
        public List<McAttendee> GetAttendees (McCalendarRoot r)
        {
            NachoCore.NachoAssert.True (r.Id > 0);
            string query = "select * from McAttendee where parentType = ? and parentId = ?";
            var l = BackEnd.Instance.Db.Query<McAttendee> (query, McAttendee.GetParentType (r), r.Id);
            NachoCore.NachoAssert.True (l.Count >= 0);
            return l;
        }

        /// <param name="parentType">CALENDAR or EXCEPTION</param>
        /// <param name="parentId">Id field from McCalendar or NcException</param>
        public List<McCalendarCategory> GetCategories (McCalendarRoot r)
        {
            NachoCore.NachoAssert.True (r.Id > 0);
            string query = "select * from McCalendarCategory where parentType = ? and parentId = ?";
            var l = BackEnd.Instance.Db.Query<McCalendarCategory> (query, McCalendarCategory.GetParentType (r), r.Id);
            NachoCore.NachoAssert.True (l.Count >= 0);
            return l;
        }

        /// <summary>
        /// Gets the exceptions.
        /// </summary>
        /// <returns>The exception list for this calendar item</returns>
        /// <param name="calendar">Calendar item</param>
        public List<McException> GetExceptions (McCalendar calendar)
        {
            NachoCore.NachoAssert.True (calendar.Id > 0);
            var l = BackEnd.Instance.Db.Table<McException> ().Where (x => x.CalendarId == calendar.Id).ToList ();
            NachoCore.NachoAssert.True (l.Count >= 0);
            return l;
        }

        /// <summary>
        /// Gets the recurrences.
        /// </summary>
        /// <returns>The recurrences for this calendar item</returns>
        /// <param name="calendar">Calendar item</param>
        public List<McRecurrence> GetRecurrences (McCalendar calendar)
        {
            NachoCore.NachoAssert.True (calendar.Id > 0);
            var l = BackEnd.Instance.Db.Table<McRecurrence> ().Where (x => x.CalendarId == calendar.Id).ToList ();
            NachoCore.NachoAssert.True (l.Count >= 0);
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
            NachoCore.NachoAssert.True (null != c);
            List<McAttendee> attendees = GetAttendees (c);

            // Delete the old
            foreach (var attendee in attendees) {
                attendee.Delete ();
            }

            // Add the new, if any
            NachoCore.NachoAssert.True (null != c.attendees);

            // Add the new
            foreach (var attendee in c.attendees) {
                if (attendee.Id > 0) {
                    int r = attendee.Update ();
                    NachoCore.NachoAssert.True (0 < r);
                } else {
                    attendee.ParentId = c.Id;
                    attendee.ParentType = McAttendee.GetParentType (c);
                    int r = attendee.Insert ();
                    NachoCore.NachoAssert.True (0 < r);
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
            NachoCore.NachoAssert.True (null != c);
            List<McCalendarCategory> categories = GetCategories (c);

            // Delete the old
            foreach (var category in categories) {
                category.Delete ();
            }

            // Add the new, if any
            NachoCore.NachoAssert.True (null != c.categories);

            // Add the new
            foreach (var category in c.categories) {
                if (category.Id > 0) {
                    int r = category.Update ();
                    NachoCore.NachoAssert.True (0 < r);
                } else {
                    category.ParentId = McCalendarCategory.GetParentType (c);
                    int r = category.Insert ();
                    NachoCore.NachoAssert.True (0 < r);
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
            NachoCore.NachoAssert.True (null != c);
            List<McException> exceptions = GetExceptions (c);

            // Delete the old
            foreach (var exception in exceptions) {
                exception.Delete ();
            }

            // Add the new, if any
            NachoCore.NachoAssert.True (null != c.exceptions);

            // Add the new
            foreach (var exception in c.exceptions) {
                if (exception.Id > 0) {
                    int r = exception.Update ();
                    NachoCore.NachoAssert.True (0 < r);
                } else {
                    int r = exception.Insert ();
                    NachoCore.NachoAssert.True (0 < r);
                }
                MergeAttendees (exception);
                MergeCategories (exception);
            }
        }

        public void MergeRecurrences (McCalendar c)
        {
            // Get the old list
            NachoCore.NachoAssert.True (null != c);
            List<McRecurrence> recurrences = GetRecurrences (c);

            // Delete the old
            foreach (var recurrence in recurrences) {
                recurrence.Delete ();
            }

            // Add the new, if any
            NachoCore.NachoAssert.True (null != c.recurrences);

            // Add the new
            foreach (var recurrence in c.recurrences) {
                if (recurrence.Id > 0) {
                    int r = recurrence.Update ();
                    NachoCore.NachoAssert.True (0 < r);
                } else {
                    int r = recurrence.Insert ();
                    NachoCore.NachoAssert.True (0 < r);
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
            NachoCore.NachoAssert.True (null != exception);

            var attendees = GetAttendees (exception);
            NachoCore.NachoAssert.True (null != attendees);

            foreach (var attendee in attendees) {
                attendee.Delete ();
            }

            var categories = GetCategories (exception);
            NachoCore.NachoAssert.True (null != categories);

            foreach (var category in categories) {
                category.Delete ();
            }

            exception.Delete ();
        }
    }
}
   
