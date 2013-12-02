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
        // public for unit tests
        public void AddCalendarItem (XElement command, NcFolder folder)
        {
            // <ApplicationData>...</ApplicationData>
            var appData = command.Element (m_ns + Xml.AirSync.ApplicationData);
            Log.Info (Log.LOG_CALENDAR, "AddEvent\n{0}", appData.ToString ());
            ProcessCalendarItem (appData, folder);
        }
        // public for unit tests
        public void UpdateCalendarItem (XElement command, NcFolder folder)
        {
            // <ApplicationData>...</ApplicationData>
            Log.Info (Log.LOG_CALENDAR, "UpdateEvent\n{0}", command.ToString ());
            var appData = command.Element (m_ns + Xml.AirSync.ApplicationData);
            ProcessCalendarItem (appData, folder);
        }

        public void ProcessCalendarItem (XElement command, NcFolder folder)
        {
            // Convert the event to an NcCalendar
            var h = new AsHelpers ();
            var r = h.CreateNcCalendarFromXML (m_ns, command);
            NcCalendar c = (NcCalendar)r.GetObject ();

            System.Diagnostics.Trace.Assert (r.isOK ());
            System.Diagnostics.Trace.Assert (null != c);

            // Look up the event by UID
            NcCalendar u = null;

            try {
                u = DataSource.Owner.Db.Get<NcCalendar> (x => x.UID == c.UID);
            } catch (Exception e) {
                Log.Info ("ProcessCalendarItem:\n{0}", e.ToString ());
            }

            // If there is no match, insert the new event.
            if (null == u) {
                NcResult ir = DataSource.Owner.Db.Insert (c);
                System.Diagnostics.Trace.Assert (ir.isOK ());
                Int64 calendarId = (Int64)ir.GetObject ();
                AddOrUpdateAttendees (calendarId, c.attendees);
                return;
            }

            // If there's a match, merge new into old, and update.

            // TODO: Merge the calendar entry
            NcResult ur = DataSource.Owner.Db.Update (u);
            System.Diagnostics.Trace.Assert (ur.isOK ());

            // Fetch the attendees
            u.attendees = GetAttendees (u);
            System.Diagnostics.Trace.Assert (null != u.attendees);

            // TODO: Merge 'em
            List<NcAttendee> m = u.attendees.Union (c.attendees).ToList ();
            AddOrUpdateAttendees (u.Id, m);

        }

        public NcResult AddOrUpdateAttendees (Int64 calendarId, List<NcAttendee> attendees)
        {
            if (null == attendees) {
                return NcResult.OK ();
            }
            foreach (var attendee in attendees) {
                if (-1 == attendee.Id) {
                    attendee.CalendarId = calendarId;
                    DataSource.Owner.Db.Insert (attendee);
                } else {
                    System.Diagnostics.Trace.Assert (calendarId == attendee.CalendarId);
                    DataSource.Owner.Db.Update (attendee);
                }
            }
            return NcResult.OK ();
        }

        public List<NcAttendee> GetAttendees(NcCalendar calendar)
        {
            System.Diagnostics.Trace.Assert (calendar.Id >= 0);
            var l = DataSource.Owner.Db.Table<NcAttendee> ().Where (x => x.CalendarId == calendar.Id).ToList ();
            System.Diagnostics.Trace.Assert (l.Count >= 0);
            return l;
        }

    }
}
