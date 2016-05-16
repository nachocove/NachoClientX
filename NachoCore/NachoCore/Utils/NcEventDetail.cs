//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Linq;
using NachoCore.ActiveSync;

namespace NachoCore.Utils
{
    public class NcEventDetail
    {
        private McEvent ev;
        private McAbstrCalendarRoot specific;
        private McCalendar series;
        private McAccount account;

        private bool hasBeenEdited;

        public NcEventDetail (McEvent occurrence)
        {
            Initialize (occurrence);
        }

        protected NcEventDetail ()
        {
        }

        protected void Initialize (McEvent occurrence)
        {
            ev = occurrence;
            account = McAccount.QueryById<McAccount> (occurrence.AccountId);
            hasBeenEdited = false;
            Refresh ();
        }

        protected void Initialize (McEvent occurrence, McAbstrCalendarRoot specific, McCalendar series, McAccount account)
        {
            this.ev = occurrence;
            this.specific = specific;
            this.series = series;
            this.account = account;
            hasBeenEdited = false;
        }

        public virtual void Refresh ()
        {
            series = McCalendar.QueryById<McCalendar> (ev.CalendarId);
            if (0 == ev.ExceptionId) {
                specific = series;
            } else {
                specific = McException.QueryById<McException> (ev.ExceptionId);
            }
        }

        public virtual bool IsValid {
            get {
                return null != ev && null != specific && null != series && null != account;
            }
        }

        public McEvent Occurrence {
            get {
                return ev;
            }
        }

        /// <summary>
        /// The McException for this occurrence, if there is one. Otherwise, the McCalendar item.
        /// </summary>
        public McAbstrCalendarRoot SpecificItem {
            get {
                return specific;
            }
        }

        /// <summary>
        /// The McCalendar item behind this event, even if this occurrence is represented by a McException.
        /// </summary>
        public McCalendar SeriesItem {
            get {
                return series;
            }
        }

        public McAccount Account {
            get {
                return account;
            }
        }

        /// <summary>
        /// The UI needs to tell the NcEventDetail if the event has been edited, because that affects
        /// how the start and end times are calculated.
        /// </summary>
        public bool HasBeenEdited {
            set {
                hasBeenEdited = value;
            }
        }

        public bool IsRecurring {
            get {
                return 0 != series.recurrences.Count;
            }
        }

        public bool IsAppointment {
            get {
                return !series.MeetingStatusIsSet || NcMeetingStatus.Appointment == series.MeetingStatus;
            }
        }

        public bool IsOrganizer {
            get {
                return IsAppointment ||
                    NcMeetingStatus.MeetingOrganizer == series.MeetingStatus ||
                    NcMeetingStatus.MeetingOrganizerCancelled == series.MeetingStatus;
            }
        }

        /// <summary>
        /// Does the meeting have an organizer that is not the current user?
        /// </summary>
        public bool HasNonSelfOrganizer {
            get {
                return !string.IsNullOrEmpty (series.OrganizerEmail) && !IsOrganizer;
            }
        }

        /// <summary>
        /// Is the user allowed to edit this event?
        /// </summary>
        public virtual bool CanEdit {
            get {
                return IsOrganizer &&
                    !IsRecurring &&
                    account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter) &&
                    (0 == series.attendees.Count || McAccount.AccountTypeEnum.Device != account.AccountType);
            }
        }

        /// <summary>
        /// Is the user allowed to change the reminder for the event?  (In some cases,
        /// the reminder can be changed even if the event cannot otherwise be edited.)
        /// </summary>
        public virtual bool CanChangeReminder {
            get {
                return account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter) &&
                    (McAccount.AccountTypeEnum.Device != account.AccountType || !IsRecurring);
            }
        }

        /// <summary>
        /// Should the "Cancel Meeting" button be shown?
        /// </summary>
        public virtual bool ShowCancelMeetingButton {
            get {
                return IsOrganizer &&
                    IsRecurring &&
                    0 != series.attendees.Count &&
                    account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter) &&
                    McAccount.AccountTypeEnum.Device != account.AccountType;
            }
        }

        public virtual DateTime StartTime {
            get {
                if (!hasBeenEdited || IsRecurring) {
                    return ev.GetStartTimeUtc ();
                }
                if (!specific.AllDayEvent) {
                    return specific.StartTime;
                }
                if (specific.StartTime <= ev.GetStartTimeUtc () && ev.GetStartTimeUtc () < specific.EndTime) {
                    return ev.GetStartTimeUtc ();
                }
                return DateTime.SpecifyKind (
                    CalendarHelper.ConvertTimeFromUtc (specific.StartTime, new AsTimeZone (specific.TimeZone).ConvertToSystemTimeZone ()).Date,
                    DateTimeKind.Local).ToUniversalTime ();
            }
        }
        public virtual DateTime EndTime {
            get {
                if (!hasBeenEdited || IsRecurring) {
                    return ev.GetEndTimeUtc ();
                }
                if (!specific.AllDayEvent) {
                    return specific.EndTime;
                }
                if (specific.StartTime <= ev.GetStartTimeUtc () && ev.GetStartTimeUtc () < specific.EndTime) {
                    return ev.GetEndTimeUtc ();
                }
                return DateTime.SpecifyKind (
                    CalendarHelper.ConvertTimeFromUtc (specific.StartTime, new AsTimeZone (specific.TimeZone).ConvertToSystemTimeZone ()).Date,
                    DateTimeKind.Local).ToUniversalTime ().AddDays (1);
            }
        }

        public string DateString {
            get {
                return Pretty.LongFullDate (StartTime);
            }
        }

        public string DurationString {
            get {
                return ComputeDurationString (specific, StartTime, EndTime);
            }
        }

        public string RecurrenceString {
            get {
                return Pretty.MakeRecurrenceString (series.recurrences);
            }
        }

        public string ReminderString {
            get {
                return Pretty.ReminderString (specific.HasReminder (), specific.GetReminder ());
            }
        }

        public virtual string CalendarNameString {
            get {
                string folderName = "(Unknown)";
                var folder = McFolder.QueryByFolderEntryId<McCalendar> (series.AccountId, series.Id).FirstOrDefault ();
                if (null != folder) {
                    folderName = folder.DisplayName;
                }
                var accountName = account.DisplayName;
                if (string.IsNullOrEmpty (accountName) || accountName == folderName) {
                    return folderName;
                } else {
                    return string.Format ("{0} : {1}", accountName, folderName);
                }
            }
        }

        private static string ComputeDurationString (McAbstrCalendarRoot cal, DateTime startTime, DateTime endTime)
        {
            if (cal.AllDayEvent) {
                if ((cal.EndTime - cal.StartTime) > TimeSpan.FromDays (1)) {
                    return string.Format ("All day from {0} through {1}",
                        Pretty.MediumFullDate (GetStartTime (cal)),
                        Pretty.MediumFullDate (CalendarHelper.ReturnAllDayEventEndTime (GetEndTime (cal))));
                } else {
                    return "All day event";
                }
            } else {
                if (startTime.ToLocalTime ().Date == endTime.ToLocalTime ().Date) {
                    return string.Format ("from {0} until {1}",
                        Pretty.Time (startTime), Pretty.Time (endTime));
                } else {
                    return string.Format ("from {0} until {1}",
                        Pretty.Time (startTime), Pretty.MediumFullDateTime (endTime));
                }
            }
        }

        public static DateTime GetStartTime (McAbstrCalendarRoot cal)
        {
            if (!cal.AllDayEvent) {
                return cal.StartTime;
            }
            return DateTime.SpecifyKind (
                CalendarHelper.ConvertTimeFromUtc (cal.StartTime, new AsTimeZone (cal.TimeZone).ConvertToSystemTimeZone ()).Date,
                DateTimeKind.Local).ToUniversalTime ();
        }

        public static DateTime GetEndTime (McAbstrCalendarRoot cal)
        {
            if (!cal.AllDayEvent) {
                return cal.EndTime;
            }
            return DateTime.SpecifyKind (
                CalendarHelper.ConvertTimeFromUtc (cal.EndTime, new AsTimeZone (cal.TimeZone).ConvertToSystemTimeZone ()).Date,
                DateTimeKind.Local).ToUniversalTime ();
        }

        public static string GetDateString (McAbstrCalendarRoot cal)
        {
            return Pretty.LongFullDate (GetStartTime (cal));
        }

        public static string GetDurationString (McAbstrCalendarRoot cal)
        {
            return ComputeDurationString (cal, GetStartTime (cal), GetEndTime (cal));
        }

        public static string GetRecurrenceString (McMeetingRequest cal)
        {
            return Pretty.MakeRecurrenceString (cal.recurrences);
        }
    }
}

