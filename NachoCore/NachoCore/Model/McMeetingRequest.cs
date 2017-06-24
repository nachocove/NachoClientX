//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using System.Text;

namespace NachoCore.Model
{
    public enum NcInstanceType
    {
        SingleAppointment = 0,
        MasterRecurringAppointment = 1,
        SingleInstanceRecurringAppointment = 2,
        ExceptionToRecurringAppointment = 3,
    };

    public enum NcMeetingMessageType
    {
        SilentUpdate = 0,
        InitialMeetingRequest = 1,
        FullUpdate = 2,
        InformationalUpdate = 3,
        Outdated = 4,
        DelegatorCopy = 5,
        DelegatedMeetingRequest = 6,
    };

    public class McMeetingRequest : McAbstrCalendarRoot
    {

        // AllDayEvent - in McAbstrClassRoot
        // StartTime - in McAbstrClassRoot
        // DtStamp - in McAbstrClassRoot
        // EndTime - in McAbstrClassRoot
        // InstanceType
        // Location - in McAbstrClassRoot
        // Organizer
        // RecurrenceId
        // Reminder - in McAbstrClassRoot
        // ResponseRequested - in McAbstrClassRoot
        // Recurrences - in McAbstrClassRoot
        // Sensitivity - in McAbstrClassRoot
        // BusyStatus - in McAbstrClassRoot
        // TimeZone - in McAbstrClassRoot
        // GlobalObjId
        // DisallowNewTimeProposal - in McAbstrClassRoot
        // MeetingMessageType

        [Indexed]
        public int EmailMessageId { get; set; }

        protected bool HasReadAncillaryData;

        public McMeetingRequest () : base ()
        {
            HasReadAncillaryData = false;
        }

        public NcInstanceType InstanceType { get; set; }

        public string Organizer { get; set; }

        public DateTime RecurrenceId { get; set; }

        public string GlobalObjId { get; set; }

        public NcMeetingMessageType MeetingMessageType { get; set; }


        private McCalendar _Calendar;
        [Ignore]
        public McCalendar Calendar {
            get {
                if (_Calendar == null) {
                    _Calendar = McCalendar.QueryByUID (AccountId, GetUID ());
                }
                return _Calendar;
            }
        }

        // Recurrences that are stored in the database.
        private List<McRecurrence> dbRecurrences = null;
        // Recurrences that were set by the app, either the UI or sync.  They don't get saved to the database
        // until Insert() or Update() is called.
        private IList<McRecurrence> appRecurrences = null;

        [Ignore]
        public IList<McRecurrence> recurrences {
            get {
                return GetAncillaryCollection (appRecurrences, ref dbRecurrences, ReadDbRecurrences);
            }
            set {
                NcAssert.NotNull (value, "To clear the recurrences, use an empty list instead of null");
                appRecurrences = value;
            }
        }

        private List<McRecurrence> ReadDbRecurrences ()
        {
            return NcModel.Instance.Db.Table<McRecurrence> ()
                .Where (x => x.MeetingRequestId == this.Id).ToList ();
        }

        private void DeleteDbRecurrences ()
        {
            DeleteAncillaryCollection (ref dbRecurrences, ReadDbRecurrences);
        }

        private void SaveRecurrences ()
        {
            SaveAncillaryCollection (ref appRecurrences, ref dbRecurrences, ReadDbRecurrences, (McRecurrence recurrence) => {
                recurrence.MeetingRequestId = this.Id;
            }, (McRecurrence recurrence) => {
                return recurrence.MeetingRequestId == this.Id;
            });
        }

        private void InsertRecurrences ()
        {
            InsertAncillaryCollection (ref appRecurrences, ref dbRecurrences, (McRecurrence recurrence) => {
                recurrence.MeetingRequestId = this.Id;
            });
        }

        /// <summary>
        /// Convert the GlobalObjId into a UID that can be used to look up a calendar event.
        /// </summary>
        /// <remarks>
        /// See https://msdn.microsoft.com/en-us/library/hh338123(v=exchg.80).aspx and
        /// https://msdn.microsoft.com/en-us/library/hh338153(v=exchg.80).aspx for the conversion
        /// algorithm.
        /// </remarks>
        public override string GetUID ()
        {
            if (string.IsNullOrEmpty (GlobalObjId)) {
                return "";
            }
            byte [] bytes = Convert.FromBase64String (GlobalObjId);
            if (48 <= bytes.Length && Encoding.ASCII.GetString (bytes, 40, 8) == "vCal-Uid") {
                // It's a vCal ID.  The ID starts at index 52.  The length of the ID is 13 less than the
                // little-endian number in bytes[36..39].  (See the documentation linked to above.)
                int length = ((bytes [39] << 24) | (bytes [38] << 16) | (bytes [37] << 8) | bytes [36]) - 13;
                if (bytes.Length == 52 + length + 1 && 0 == bytes [52 + length]) {
                    return Encoding.ASCII.GetString (bytes, 52, length);
                } else {
                    Log.Error (Log.LOG_CALENDAR, "GlobalObjId has an unexpected format. It looks like a vCal ID, but the length is not correct.");
                    return "";
                }
            } else {
                // It's an Outlook ID.  Zero out bytes[16..19], then encode the entire thing as a hex string.
                if (20 <= bytes.Length) {
                    bytes [16] = 0;
                    bytes [17] = 0;
                    bytes [18] = 0;
                    bytes [19] = 0;
                }
                return HexString (bytes, 0, bytes.Length);
            }
        }

        private static char [] HexDigits = {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'
        };

        private static string HexString (byte [] bytes, int start, int length)
        {
            var builder = new StringBuilder (length * 2);
            for (int i = start; i < start + length; ++i) {
                builder.Append (HexDigits [(bytes [i] & 0xf0) >> 4]).Append (HexDigits [bytes [i] & 0x0f]);
            }
            return builder.ToString ();
        }

        public override int Insert ()
        {
            using (var capture = CaptureWithStart ("Insert")) {
                int retval = 0;
                NcModel.Instance.RunInTransaction (() => {
                    retval = base.Insert ();
                    InsertRecurrences ();
                });
                return retval;
            }
        }

        public override int Update ()
        {
            using (var capture = CaptureWithStart ("Update")) {
                int retval = 0;
                NcModel.Instance.RunInTransaction (() => {
                    retval = base.Update ();
                    SaveRecurrences ();
                });
                return retval;
            }
        }

        public override void DeleteAncillary ()
        {
            base.DeleteAncillary ();
            DeleteDbRecurrences ();
        }
    }
}

