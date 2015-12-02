//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoClient.AndroidClient;
using Android.Provider;
using Android.Content;
using Android.Database;

namespace NachoPlatform
{
    /// <summary>
    /// Access the Android device calendar database, other than synching all the events into Nacho Mail.
    /// </summary>
    public static class AndroidCalendars
    {
        private static string[] instancesProjection = new string[] {
            CalendarContract.Instances.EventId,
            CalendarContract.Instances.Begin,
            CalendarContract.Instances.End,
            CalendarContract.Instances.InterfaceConsts.AllDay,
            CalendarContract.Instances.InterfaceConsts.Uid2445,
        };
        private const int INSTANCES_EVENT_ID_INDEX = 0;
        private const int INSTANCES_BEGIN_INDEX = 1;
        private const int INSTANCES_END_INDEX = 2;
        private const int INSTANCES_ALL_DAY_INDEX = 3;
        private const int INSTANCES_UID_INDEX = 4;

        /// <summary>
        /// Create in-memory McEvent objects for all of the device events within the given date range.
        /// The McEvents that are crated will have a negative CalendarId, which is the negative value
        /// of the event's ID in the Android database.
        /// </summary>
        public static List<McEvent> GetDeviceEvents (DateTime startRange, DateTime endRange)
        {
            var resolver = MainApplication.Instance.ContentResolver;
            var uriBuilder = CalendarContract.Instances.ContentSearchUri.BuildUpon ();
            ContentUris.AppendId (uriBuilder, startRange.MillisecondsSinceEpoch ());
            ContentUris.AppendId (uriBuilder, endRange.MillisecondsSinceEpoch ());
            ICursor eventCursor;
            try {
                eventCursor = CalendarContract.Instances.Query (resolver, instancesProjection, startRange.MillisecondsSinceEpoch (), endRange.MillisecondsSinceEpoch ());
            } catch (Exception e) {
                Log.Error (Log.LOG_SYS, "Querying device events failed with {0}", e.ToString ());
                return new List<McEvent> ();
            }

            var deviceAccount = McAccount.GetDeviceAccount ().Id;

            var result = new List<McEvent> ();

            while (eventCursor.MoveToNext ()) {
                long eventId = eventCursor.GetLong (INSTANCES_EVENT_ID_INDEX);
                DateTime start = eventCursor.GetLong (INSTANCES_BEGIN_INDEX).JavaMsToDateTime ();
                DateTime end = eventCursor.GetLong (INSTANCES_END_INDEX).JavaMsToDateTime ();
                bool allDay = eventCursor.GetInt (INSTANCES_ALL_DAY_INDEX) != 0;
                string uid = eventCursor.GetString (INSTANCES_UID_INDEX);

                result.Add (new McEvent () {
                    AccountId = deviceAccount,
                    CalendarId = (int)(-eventId),
                    StartTime = start,
                    EndTime = end,
                    AllDayEvent = allDay,
                    UID = uid,
                });
            }

            return result;
        }

        private static string[] eventProjection = new string[] {
            Android.Provider.CalendarContract.Events.InterfaceConsts.Title,
            Android.Provider.CalendarContract.Events.InterfaceConsts.EventLocation,
        };
        private const int EVENT_TITLE_INDEX = 0;
        private const int EVENT_LOCATION_INDEX = 1;

        /// <summary>
        /// Get some of the details for a particular event in the Android calendar database.
        /// </summary>
        public static bool GetEventDetails (long eventId, out string title, out string location, out int colorIndex)
        {
            title = null;
            location = null;
            colorIndex = 0;
            var resolver = MainApplication.Instance.ContentResolver;
            ICursor eventCursor;
            try {
                eventCursor = resolver.Query(CalendarContract.Events.ContentUri,eventProjection,CalendarContract.Events.InterfaceConsts.Id + " = ?", new string[] { eventId.ToString() }, null, null);
            } catch (Exception e) {
                Log.Error (Log.LOG_SYS, "Looking up device details failed with {0}", e.ToString ());
                return false;
            }
            if (!eventCursor.MoveToNext ()) {
                return false;
            }
            title = eventCursor.GetString (EVENT_TITLE_INDEX);
            location = eventCursor.GetString (EVENT_LOCATION_INDEX);

            // TODO Somehow get the color from the calendar that owns this event.
            colorIndex = McFolder.GetDeviceCalendarsFolder().DisplayColor;

            return true;
        }

        /// <summary>
        /// An Android intent that will view the given event in the Android calendar app.
        /// </summary>
        public static Intent ViewEventIntent (McEvent ev)
        {
            var intent = new Intent (Intent.ActionView, ContentUris.WithAppendedId (CalendarContract.Events.ContentUri, -ev.CalendarId));
            intent.PutExtra (CalendarContract.ExtraEventBeginTime, ev.StartTime.MillisecondsSinceEpoch ());
            intent.PutExtra (CalendarContract.ExtraEventEndTime, ev.EndTime.MillisecondsSinceEpoch ());
            return intent;
        }

        /// <summary>
        /// An Android intent to create a new event using the Android calendar app.
        /// </summary>
        /// <returns>The event intent.</returns>
        public static Intent NewEventIntent ()
        {
            return NewEventOnDayIntent (DateTime.Now);
        }

        /// <summary>
        /// An Android intent to create a new event on the given day using the Android calendar app.
        /// </summary>
        public static Intent NewEventOnDayIntent (DateTime day)
        {
            var tempCal = CalendarHelper.DefaultMeeting (day);
            var intent = new Intent (Intent.ActionInsert, CalendarContract.Events.ContentUri);
            intent.PutExtra (CalendarContract.ExtraEventBeginTime, tempCal.StartTime.MillisecondsSinceEpoch ());
            intent.PutExtra (CalendarContract.ExtraEventEndTime, tempCal.EndTime.MillisecondsSinceEpoch ());
            return intent;
        }
    }

    public sealed class Calendars : IPlatformCalendars
    {
        private const int SchemaRev = 0;
        private static volatile Calendars instance;
        private static object syncRoot = new Object ();

        private Calendars ()
        {
        }

        public static Calendars Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new Calendars ();
                        }
                    }
                }
                return instance;
            }
        }

        public void AskForPermission (Action<bool> result)
        {
            // Permissions are controlled by the app's manifest.  They aren't changed at runtime.
        }

        public void GetCalendars (out IEnumerable<PlatformCalendarFolderRecord> folders, out IEnumerable<PlatformCalendarRecord> events)
        {
            // On Android, calendars are not synched.  Calendar items are accessed on demand.
            folders = null;
            events = null;
        }

        public event EventHandler ChangeIndicator;

        public NcResult Add (McCalendar contact)
        {
            return NcResult.Error ("Android Calendars.Add not yet implemented.");
        }

        public NcResult Delete (string serverId)
        {
            return NcResult.Error ("Android Calendars.Delete not yet implemented.");
        }

        public NcResult Change (McCalendar contact)
        {
            return NcResult.Error ("Android Calendars.Change not yet implemented.");
        }

        public bool AuthorizationStatus {
            get {
                return false;
            }
        }
    }
}

