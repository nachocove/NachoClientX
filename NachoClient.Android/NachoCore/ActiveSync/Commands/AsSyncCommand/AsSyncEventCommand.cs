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
        public void ServerSaysAddCalendarItem (XElement command, NcFolder folder)
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
        public void ServerSaysChangeCalendarItem (XElement command, NcFolder folder)
        {
            Log.Info (Log.LOG_CALENDAR, "ServerSaysChangeCalendarItem\n{0}", command.ToString ());
            ProcessCalendarItem (command, folder);
        }

        public void ProcessCalendarItem (XElement command, NcFolder folder)
        {
            // Convert the event to an NcCalendar
            var h = new AsHelpers ();
            var r = h.CreateNcCalendarFromXML (m_ns, command, folder);
            NcCalendar newItem = (NcCalendar)r.GetObject ();

            System.Diagnostics.Trace.Assert (r.isOK ());
            System.Diagnostics.Trace.Assert (null != newItem);

            // Look up the event by ServerId
            NcCalendar oldItem = null;

            try {
                oldItem = DataSource.Owner.Db.Get<NcCalendar> (x => x.ServerId == newItem.ServerId);
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
                return;
            }

            // For a merge, we'll update the new entry following
            // the rules stated in the docs & repeated up above.

            // Pull over the Body
            if (0 == newItem.BodyId) {
                newItem.BodyId = newItem.BodyId;
            }

            // Update the database with the new entry
            newItem.Id = oldItem.Id;
            NcResult ur = DataSource.Owner.Db.Update (oldItem);
            System.Diagnostics.Trace.Assert (ur.isOK ());

            MergeAttendees (newItem);
            MergeCategories (newItem);
        }

        public List<NcAttendee> GetAttendees (NcCalendar calendar)
        {
            System.Diagnostics.Trace.Assert (calendar.Id >= 0);
            var l = DataSource.Owner.Db.Table<NcAttendee> ().Where (x => x.CalendarId == calendar.Id).ToList ();
            System.Diagnostics.Trace.Assert (l.Count >= 0);
            return l;
        }

        public List<NcCategory> GetCategories (NcCalendar calendar)
        {
            System.Diagnostics.Trace.Assert (calendar.Id >= 0);
            var l = DataSource.Owner.Db.Table<NcCategory> ().Where (x => x.CalendarId == calendar.Id).ToList ();
            System.Diagnostics.Trace.Assert (l.Count >= 0);
            return l;
        }

        // I didn't see any fancy rules about how to merge
        // attendee lists, so taking the slow & safe road
        // of deleting the old and inserting the new.
        // TODO: Handle errors
        public void MergeAttendees (NcCalendar c)
        {
            List<NcAttendee> attendees = GetAttendees (c);

            // Delete the old
            foreach (var attendee in attendees) {
                DataSource.Owner.Db.Delete (attendee);
            }

            // Add the new, if any
            if (null == c.attendees) {
                return;
            }

            // Add the new
            foreach (var attendee in c.attendees) {
                if (attendee.Id > 0) {
                    NcResult r = DataSource.Owner.Db.Update (attendee);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                } else {
                    NcResult r = DataSource.Owner.Db.Insert (attendee);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                    attendee.Id = r.GetIndex ();
                }
            }

        }

        // I didn't see any fancy rules about how to merge
        // category lists, so taking the slow & safe road
        // of deleting the old and inserting the new.
        // TODO: Handle errors
        public void MergeCategories (NcCalendar c)
        {
            List<NcCategory> categories = GetCategories (c);

            // Delete the old
            foreach (var category in categories) {
                DataSource.Owner.Db.Delete (category);
            }

            // Add the new, if any
            if (null == c.categories) {
                return;
            }

            // Add the new
            foreach (var category in c.categories) {
                if (category.Id > 0) {
                    NcResult r = DataSource.Owner.Db.Update (category);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                } else {
                    NcResult r = DataSource.Owner.Db.Insert (category);
                    System.Diagnostics.Trace.Assert (r.isOK ());
                    category.Id = r.GetIndex ();
                }
            }
        }

    }
}
   