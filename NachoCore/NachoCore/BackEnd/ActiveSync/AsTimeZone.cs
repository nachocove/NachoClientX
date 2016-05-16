//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    // Complains that we aren't overriding GetHashCode
    #pragma warning disable 659

    /// <summary>
    /// Utilities for ActiveSync TimeZone
    /// </summary>
    public class AsTimeZone
    {
        protected byte[] binaryData;

        /// <summary>
        /// Convert Base64 uuencoded input to binary array
        /// </summary>
        /// <param name="encodedTimeZone">Encoded time zone.</param>
        public AsTimeZone (string encodedTimeZone)
        {
            binaryData = null;
            try {
                binaryData = System.Convert.FromBase64String (encodedTimeZone);
                if (binaryData.Length != (4 + 64 + 16 + 4 + 64 + 16 + 4)) {
                    Log.Error (Log.LOG_AS, "Decoded TimeZone string has the wrong length: " + binaryData.Length.ToString ());
                    binaryData = null;
                }
            } catch (System.ArgumentNullException) {
                Log.Error (Log.LOG_AS, "Encoded TimeZone string is null.");
            } catch (System.FormatException) {
                Log.Error (Log.LOG_AS, "Encoded TimeZone string is not a valid base-64 string.");
            }
            if (null == binaryData) {
                // Something went wrong.  Since the encoded time zone string was input from an external source
                // (the Exchange server), the app shouldn't crash.  Instead, set the time zone to be UTC.
                binaryData = new AsTimeZone (TimeZoneInfo.Utc, DateTime.UtcNow).binaryData;
            }
        }

        public AsTimeZone (TimeZoneInfo tzi, DateTime forEventDate)
        {
            binaryData = new byte[4 + 64 + 16 + 4 + 64 + 16 + 4];

            this.Bias = -(long)tzi.BaseUtcOffset.TotalMinutes;
            this.StandardBias = 0;
            this.StandardName = tzi.StandardName;

            TimeZoneInfo.AdjustmentRule adjustment = null;
            if (tzi.SupportsDaylightSavingTime) {
                foreach (var a in tzi.GetAdjustmentRules()) {
                    if (a.DateStart <= forEventDate && a.DateEnd > forEventDate) {
                        adjustment = a;
                        break;
                    }
                }
            }

            if (null != adjustment) {
                this.DaylightName = tzi.DaylightName;
                this.DaylightBias = -(long)adjustment.DaylightDelta.TotalMinutes;
                this.DaylightDate = SystemTime.ToSystemTime (adjustment.DaylightTransitionStart, forEventDate);
                this.StandardDate = SystemTime.ToSystemTime (adjustment.DaylightTransitionEnd, forEventDate);
            } else {
                this.DaylightName = "";
                this.DaylightBias = 0;
            }
        }

        public TimeZoneInfo ConvertToSystemTimeZone ()
        {
            try {
                string timeZoneID;
                string displayName;
                string standardName;
                if (string.IsNullOrEmpty (StandardName)) {
                    timeZoneID = "CustomID";
                    displayName = "Custom Time Zone";
                    standardName = "Standard";
                } else {
                    timeZoneID = StandardName;
                    displayName = StandardName;
                    standardName = StandardName;
                }
                string daylightName = string.IsNullOrEmpty(DaylightName) ? "Daylight" : DaylightName;

                if (0 == DaylightBias && 0 == StandardBias) {
                    // Simple case. No daylight saving time.
                    return TimeZoneInfo.CreateCustomTimeZone (
                        timeZoneID, new TimeSpan (-(Bias * TimeSpan.TicksPerMinute)), displayName, standardName);
                }
                TimeZoneInfo.TransitionTime transitionToDaylight;
                if (0 == DaylightDate.year) {
                    transitionToDaylight = TimeZoneInfo.TransitionTime.CreateFloatingDateRule (
                        new DateTime (1, 1, 1, DaylightDate.hour, DaylightDate.minute, DaylightDate.second),
                        DaylightDate.month, DaylightDate.day, (DayOfWeek)DaylightDate.dayOfWeek);
                } else {
                    transitionToDaylight = TimeZoneInfo.TransitionTime.CreateFixedDateRule (
                        new DateTime (1, 1, 1, DaylightDate.hour, DaylightDate.minute, DaylightDate.second),
                        DaylightDate.month, DaylightDate.day);
                }

                TimeZoneInfo.TransitionTime transitionToStandard;
                if (0 == StandardDate.year) {
                    transitionToStandard = TimeZoneInfo.TransitionTime.CreateFloatingDateRule (
                        new DateTime (1, 1, 1, StandardDate.hour, StandardDate.minute, StandardDate.second),
                        StandardDate.month, StandardDate.day, (DayOfWeek)StandardDate.dayOfWeek);
                } else {
                    transitionToStandard = TimeZoneInfo.TransitionTime.CreateFixedDateRule (
                        new DateTime (1, 1, 1, StandardDate.hour, StandardDate.minute, StandardDate.second),
                        StandardDate.month, StandardDate.day);
                }

                var adjustment = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule (
                    DateTime.MinValue.Date, DateTime.MaxValue.Date,
                    new TimeSpan (-((DaylightBias - StandardBias) * TimeSpan.TicksPerMinute)),
                    transitionToDaylight, transitionToStandard);

                return TimeZoneInfo.CreateCustomTimeZone (
                    timeZoneID, new TimeSpan (-((Bias + StandardBias) * TimeSpan.TicksPerMinute)),
                    displayName, standardName, daylightName,
                    new TimeZoneInfo.AdjustmentRule[] { adjustment });

            } catch (ArgumentException e) {
                // Most likely caused by malformed time zone information in
                // the event.
                Log.Error (Log.LOG_CALENDAR, "Malformed time zone information in an ActiveSync event: {0}", e.ToString ());
                return TimeZoneInfo.Utc;
            } catch (InvalidTimeZoneException e) {
                // Unlikely, but it might happen if the offsets are messed up.
                Log.Error (Log.LOG_CALENDAR, "Malformed time zone information in an ActiveSync event: {0}", e.ToString ());
                return TimeZoneInfo.Utc;
            }
        }

        public override bool Equals (System.Object obj)
        {
            // If parameter is null return false.
            if (obj == null) {
                return false;
            }
            // If parameter cannot be cast to Point return false.
            var p = obj as AsTimeZone;
            if ((System.Object)p == null) {
                return false;
            }
            // Compare arrays
            if (p.binaryData.Length != binaryData.Length) {
                return false;
            }
            for (int i = 0; i < binaryData.Length; i++) {
                if (p.binaryData [i] != binaryData [i]) {
                    return false;
                }
            }
            return true;
        }

        public string toEncodedTimeZone ()
        {
            NcAssert.True (binaryData.Length == (4 + 64 + 16 + 4 + 64 + 16 + 4));
            return System.Convert.ToBase64String (binaryData);
        }

        public string StandardName {
            get {
                return ExtractStringFromBinaryData (binaryData, 4, 64);
            }
            set {
                InsertStringIntoBinaryData (value, binaryData, 4, 64);
            }
        }

        public string DaylightName {
            get {
                return ExtractStringFromBinaryData (binaryData, 4 + 64 + 16 + 4, 64);
            }
            set {
                InsertStringIntoBinaryData (value, binaryData, 4 + 64 + 16 + 4, 64);
            }
        }

        public long Bias {
            get {
                return  ExtractLongFromBinaryData (binaryData, 0);
            }
            set {
                InsertLongIntoBinaryData (value, binaryData, 0);
            }
        }

        public SystemTime StandardDate {
            get {
                return ExtractSystemTimeFromBinaryData (binaryData, 4 + 64);
            }
            set {
                InsertSystemTimeIntoBinaryData (value, binaryData, 4 + 64);
            }
        }

        public long StandardBias {
            get {
                return ExtractLongFromBinaryData (binaryData, 4 + 64 + 16);
            }
            set {
                InsertLongIntoBinaryData (value, binaryData, 4 + 64 + 16);
            }
        }

        public SystemTime DaylightDate {
            get {
                return ExtractSystemTimeFromBinaryData (binaryData, 4 + 64 + 16 + 4 + 64);
            }
            set {
                InsertSystemTimeIntoBinaryData (value, binaryData, 4 + 64 + 16 + 4 + 64);
            }
        }

        public long DaylightBias {
            get {
                return ExtractLongFromBinaryData (binaryData, 4 + 64 + 16 + 4 + 64 + 16);
            }
            set {
                InsertLongIntoBinaryData (value, binaryData, 4 + 64 + 16 + 4 + 64 + 16);
            }
        }

        public class SystemTime
        {
            public int year;
            public int month;
            public int dayOfWeek;
            public int day;
            public int hour;
            public int minute;
            public int second;
            public int milliseconds;

            public SystemTime ()
            {
            }

            public SystemTime (int year, int month, int dayOfWeek, int day, int hour, int minute, int second, int millisecond)
            {
                this.year = year;
                this.month = month;
                this.dayOfWeek = ((0 < dayOfWeek) ? dayOfWeek : 0);  // no -1 allowed
                this.day = day;
                this.hour = hour;
                this.minute = minute;
                this.second = second;
                this.milliseconds = millisecond;
            }

            /// <summary>
            ///  To select the correct day in the month, set the wYear member to zero,
            /// the wHour and wMinute members to the transition time, the wDayOfWeek member
            /// to the appropriate weekday, and the wDay member to indicate the occurrence
            /// of the day of the week within the month (1 to 5, where 5 indicates the final
            /// occurrence during the month if that day of the week does not occur 5 times).
            /// </summary>

            public static SystemTime ToSystemTime(TimeZoneInfo.TransitionTime t, DateTime forEventDate)
            {
                if (t.IsFixedDateRule) {
                    return new SystemTime (forEventDate.Year, t.Month, 0, t.Day, t.TimeOfDay.Hour, t.TimeOfDay.Minute, t.TimeOfDay.Second, t.TimeOfDay.Millisecond);
                } else {
                    return new SystemTime (0, t.Month, (int)t.DayOfWeek, t.Week, t.TimeOfDay.Hour, t.TimeOfDay.Minute, t.TimeOfDay.Second, t.TimeOfDay.Millisecond);
                }
            }

            public override bool Equals (System.Object obj)
            {
                // If parameter is null return false.
                if (obj == null) {
                    return false;
                }
                // If parameter cannot be cast to Point return false.
                var p = obj as SystemTime;
                if ((System.Object)p == null) {
                    return false;
                }
                if (p.year != year) {
                    return false;
                }
                if (p.month != month) {
                    return false;
                }
                if (p.dayOfWeek != dayOfWeek) {
                    return false;
                }
                if (p.day != day) {
                    return false;
                }
                if (p.hour != hour) {
                    return false;
                }
                if (p.minute != minute) {
                    return false;
                }
                if (p.second != second) {
                    return false;
                }
                if (p.milliseconds != milliseconds) {
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Extracts a string field from a TimeZone record.
        /// The value of this field is an array of 32 WCHARs
        /// Any unused WCHARs in the array MUST be set to 0x0000.
        /// </summary>
        /// <returns>The string from the time zone.</returns>
        /// <param name="binaryData">The packaged string</param>
        /// <param name="start">Starting offset of the first character</param>
        /// <param name="fieldLength">Length of the field.</param>
        protected string ExtractStringFromBinaryData (byte[] binaryData, int start, int fieldLength)
        {
            NcAssert.True ((start + fieldLength) <= binaryData.Length);
            String field = System.Text.UnicodeEncoding.Unicode.GetString (binaryData, start, fieldLength);
            int index = field.IndexOf (System.Convert.ToChar (0)); // trim trailing null padding
            if (index < 0) { // no nulls
                return field;
            } else {
                return field.Substring (0, index);
            }
        }

        protected void InsertStringIntoBinaryData (string value, byte[] binaryData, int start, int fieldLength)
        {
            NcAssert.True ((start + fieldLength) <= binaryData.Length);
            var asBytes = System.Text.UnicodeEncoding.Unicode.GetBytes (value);
            for (int i = 0; i < fieldLength; i++) {
                if (i < asBytes.Length) {
                    binaryData [start + i] = asBytes [i];
                } else {
                    binaryData [start + i] = 0;
                }
            }
        }

        protected int ExtractLongFromBinaryData (byte[] binaryData, int start)
        {
            int value = binaryData [start + 3] << 24;
            value |= binaryData [start + 2] << 16;
            value |= binaryData [start + 1] << 8;
            value |= binaryData [start];
            return value;
        }

        protected void InsertLongIntoBinaryData (long value, byte[] binaryData, int start)
        {
            binaryData [start + 3] = (byte)((value >> 24) & 0xff);
            binaryData [start + 2] = (byte)((value >> 16) & 0xff);
            binaryData [start + 1] = (byte)((value >> 8) & 0xff);
            binaryData [start] = (byte)(value & 0xff);
        }

        protected int ExtractShortFromBinaryData (byte[] binaryData, int start)
        {
            int value = binaryData [start + 1] << 8;
            value |= binaryData [start];
            return value;
        }

        protected void InsertShortIntoBinaryData (long value, byte[] binaryData, int start)
        {
            binaryData [start + 1] = (byte)((value >> 8) & 0xff);
            binaryData [start] = (byte)(value & 0xff);
        }

        ///        typedef struct _SYSTEMTIME {
        ///            WORD wYear;
        ///            WORD wMonth;
        ///            WORD wDayOfWeek;
        ///            WORD wDay;
        ///            WORD wHour;
        ///            WORD wMinute;
        ///            WORD wSecond;
        ///            WORD wMilliseconds;
        ///        } SYSTEMTIME, *PSYSTEMTIME;
        /// <summary>
        /// Extracts a SystemTime from a TimeZone record.
        /// </summary>
        protected SystemTime ExtractSystemTimeFromBinaryData (byte[] binaryData, int start)
        {
            var value = new SystemTime ();
            value.year = ExtractShortFromBinaryData (binaryData, start);
            value.month = ExtractShortFromBinaryData (binaryData, start + 2);
            value.dayOfWeek = ExtractShortFromBinaryData (binaryData, start + 4);
            value.day = ExtractShortFromBinaryData (binaryData, start + 6);
            value.hour = ExtractShortFromBinaryData (binaryData, start + 8);
            value.minute = ExtractShortFromBinaryData (binaryData, start + 10);
            value.milliseconds = ExtractShortFromBinaryData (binaryData, start + 12);
            return value;
        }

        protected SystemTime InsertSystemTimeIntoBinaryData (SystemTime value, byte[] binaryData, int start)
        {
            InsertShortIntoBinaryData (value.year, binaryData, start);
            InsertShortIntoBinaryData (value.month, binaryData, start + 2);
            InsertShortIntoBinaryData (value.dayOfWeek, binaryData, start + 4);
            InsertShortIntoBinaryData (value.day, binaryData, start + 6);
            InsertShortIntoBinaryData (value.hour, binaryData, start + 8);
            InsertShortIntoBinaryData (value.minute, binaryData, start + 10);
            InsertShortIntoBinaryData (value.milliseconds, binaryData, start + 12);
            return value;
        }

        // Enable this to dump AsTimeZone objects.
        #if FOR_DEBUGGING
        private static string DumpSystemTime (SystemTime time)
        {
            return string.Format ("Y:{0} M:{1} DoW:{2} D:{3} H:{4} m:{5} ms:{6}",
                time.year, time.month, time.dayOfWeek, time.day, time.hour, time.minute, time.milliseconds);
        }

        public static void DumpTimeZone (AsTimeZone tz)
        {
            Log.Info (Log.LOG_CALENDAR, "Time zone info: Standard={0} Daylight={1} Bias={2} StandardBias={3} DaylightBias={4}",
                tz.StandardName, tz.DaylightName, tz.Bias, tz.StandardBias, tz.DaylightBias);
            Log.Info (Log.LOG_CALENDAR, "Time zone info: Daylight start: {0}", DumpSystemTime (tz.DaylightDate));
            Log.Info (Log.LOG_CALENDAR, "Time zone info: Daylight end:   {0}", DumpSystemTime (tz.StandardDate));
        }
        #endif
    }
}

