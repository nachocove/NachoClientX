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
            try {
                binaryData = System.Convert.FromBase64String (encodedTimeZone);
            } catch (System.ArgumentNullException) {
                Log.Warn (Log.LOG_AS, "Encoded TimeZone string is null.");
                throw;
            } catch (System.FormatException) {
                Log.Warn (Log.LOG_AS, "Encoded TimeZone string length is not 4 or is not an even multiple of 4.");
                throw;
            }
            if (binaryData.Length != (4 + 64 + 16 + 4 + 64 + 16 + 4)) {
                Log.Warn (Log.LOG_AS, "Decoded TimeZone string length is wrong: " + binaryData.Length.ToString ());
                throw new System.FormatException ();
            }
        }

        public AsTimeZone (TimeZoneInfo tzi, DateTime forEventDate)
        {
            binaryData = new byte[4 + 64 + 16 + 4 + 64 + 16 + 4];

            TimeZoneInfo.AdjustmentRule adjustment = null;

            var adjustmentRules = tzi.GetAdjustmentRules ();
            foreach (var a in adjustmentRules) {
                if ((a.DateStart <= forEventDate) && (a.DateEnd >= forEventDate)) {
                    adjustment = a;
                    break;
                }
            }
            if (null == adjustment) {
                if ((null == adjustmentRules) || (0 == adjustmentRules.Length)) {
                    return;
                }
            }
            adjustment = adjustmentRules [adjustmentRules.Length - 1];

            this.Bias = -(long)tzi.BaseUtcOffset.TotalMinutes;
            this.StandardName = tzi.StandardName;
            if (tzi.SupportsDaylightSavingTime) {
                this.DaylightName = tzi.DaylightName;
            } else {
                this.DaylightName = "";
            }

            if (null == adjustment) {
                return;
            }

            if (tzi.SupportsDaylightSavingTime) {
                this.StandardBias = 0;
                this.DaylightBias = -(long)adjustment.DaylightDelta.TotalMinutes;
                this.StandardDate = SystemTime.ToSystemTime (adjustment.DaylightTransitionEnd, forEventDate);
                this.DaylightDate = SystemTime.ToSystemTime (adjustment.DaylightTransitionStart, forEventDate);
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
    }
}

