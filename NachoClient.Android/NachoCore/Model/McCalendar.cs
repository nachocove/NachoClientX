//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public partial class McCalendar : McAbstrCalendarRoot
    {
        /// Implicit [Ignore]
        private List<McException> DbExceptions;
        /// Implicit [Ignore]
        private List<McRecurrence> DbRecurrences;

        /// ActiveSync or Device
        public McAbstrItem.ItemSource Source { get; set; }

        /// Set only for Device calendars.
        public string DeviceUniqueId { get; set; }

        /// Set only for Device calendars.
        public DateTime DeviceCreation { get; set; }

        /// Set only for Device calendars.
        public DateTime DeviceLastUpdate { get; set; }

        /// Name of the creator of the calendar item (optional). Calendar only.
        [MaxLength (256)]
        public string OrganizerName { get; set; }

        /// Email of the creator of the calendar item (optional). Calendar only.
        [MaxLength (256)]
        public string OrganizerEmail { get; set; }

        /// Unique 300 digit hexidecimal ID generated by the client. Calendar only.
        [MaxLength (300)]
        public string UID { get; set; }

        /// Recurrences are generated into the McEvent table thru this date.
        public DateTime RecurrencesGeneratedUntil { get; set; }

        public override ClassCodeEnum GetClassCode ()
        {
            return McAbstrFolderEntry.ClassCodeEnum.Calendar;
        }

        protected bool HasReadAncillaryData;

        public McCalendar () : base ()
        {
            HasReadAncillaryData = false;
            DbExceptions = new List<McException> ();
            DbRecurrences = new List<McRecurrence> ();
        }

        [Ignore]
        public List<McException> exceptions {
            get {
                ReadAncillaryData ();
                return DbExceptions;
            }
            set {
                ReadAncillaryData ();
                DbExceptions = value;
            }
        }

        [Ignore]
        public List<McRecurrence> recurrences {
            get {
                ReadAncillaryData ();
                return DbRecurrences;
            }
            set {
                ReadAncillaryData ();
                DbRecurrences = value;
            }
        }

        public override int Insert ()
        {
            // FIXME db transaction.
            int retval = base.Insert ();
            InsertAncillaryData ();
            return retval;
        }

        private NcResult InsertAncillaryData ()
        {
            foreach (var e in exceptions) {
                e.Id = 0;
                e.AccountId = this.AccountId; // McAbstrObjectPerAcc requires AccountId
                e.CalendarId = this.Id;
                e.Insert ();
            }
            foreach (var r in recurrences) {
                r.Id = 0;
                r.CalendarId = this.Id;
                r.Insert ();
            }
            return NcResult.OK ();
        }

        public override int Update ()
        {
            // FIXME db transaction
            int retval = base.Update ();
            UpdateAncillaryData (NcModel.Instance.Db);
            return retval;
        }

        public void UpdateAncillaryData (SQLiteConnection db)
        {
            ReadAncillaryData ();
            DeleteAncillaryDataFromDB (db);
            InsertAncillaryData ();
        }

        private NcResult ReadAncillaryData ()
        {
            if (HasReadAncillaryData) {
                return NcResult.OK ();
            }
            if (0 == Id) {
                HasReadAncillaryData = true;
                return NcResult.OK ();
            }
            DbExceptions = NcModel.Instance.Db.Table<McException> ().Where (x => x.CalendarId == Id).ToList ();
            DbRecurrences = NcModel.Instance.Db.Table<McRecurrence> ().Where (x => x.CalendarId == Id).ToList ();
            HasReadAncillaryData = true;
            return NcResult.OK ();
        }

        public override void DeleteAncillary ()
        {
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            base.DeleteAncillary ();
            DeleteAncillaryDataFromDB (NcModel.Instance.Db);
        }

        private NcResult DeleteAncillaryDataFromDB (SQLiteConnection db)
        {
            NcAssert.True (0 != Id);
            var exceptions = db.Table<McException> ().Where (x => x.CalendarId == Id).ToList ();
            foreach (var e in exceptions) {
                e.Delete ();
            }
            var recurrences = db.Table<McRecurrence> ().Where (x => x.CalendarId == Id).ToList ();
            foreach (var r in recurrences) {
                r.Delete ();
            }
            return NcResult.OK ();
        }

        public static McCalendar QueryByDeviceUniqueId (string deviceUniqueId)
        {
            var account = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Device).Single ();
            return NcModel.Instance.Db.Table<McCalendar> ().Where (x => 
                x.DeviceUniqueId == deviceUniqueId &&
            x.AccountId == account.Id
            ).SingleOrDefault ();
        }

        public static McCalendar QueryByUID (string UID)
        {
            return NcModel.Instance.Db.Table<McCalendar> ().Where (x => 
                x.UID == UID
            ).SingleOrDefault ();
        }

        public static List<McCalendar> QueryOutOfDateRecurrences (DateTime generateUntil)
        {
            return NcModel.Instance.Db.Table<McCalendar> ().Where (x => x.RecurrencesGeneratedUntil < generateUntil).ToList ();
        }

        public void DeleteRelatedEvents ()
        {
            var list = NcModel.Instance.Db.Table<McEvent> ().Where (x => x.CalendarId == Id).ToList ();
            foreach (var e in list) {
                e.Delete ();
            }
        }

        public override int Delete ()
        {
            DeleteRelatedEvents ();
            return base.Delete ();
        }
    }
}

