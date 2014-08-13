//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;

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

        public int EmailMessageId { get; set; }

        protected bool HasReadAncillaryData;

        [Ignore]
        private List<McRecurrence> DbRecurrences { get; set; }

        public McMeetingRequest () : base ()
        {
            HasReadAncillaryData = false;
            DbRecurrences = new List<McRecurrence> ();
        }

        public NcInstanceType InstanceType { get; set; }

        public string Organizer { get; set; }

        public DateTime RecurrenceId { get; set; }

        public string GlobalObjId { get; set; }

        public NcMeetingMessageType MeetingMessageType { get; set; }

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
            foreach (var r in recurrences) {
                r.Id = 0;
                r.MeetingRequestId = this.Id;
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
            HasReadAncillaryData = true;
            DbRecurrences = NcModel.Instance.Db.Table<McRecurrence> ().Where (x => x.MeetingRequestId == Id).ToList ();
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
            var recurrences = db.Table<McRecurrence> ().Where (x => x.MeetingRequestId == Id).ToList ();
            foreach (var r in recurrences) {
                r.Delete ();
            }
            return NcResult.OK ();
        }

        public static ClassCodeEnum GetClassCode ()
        {
            return McAbstrFolderEntry.ClassCodeEnum.MeetingRequest;
        }
    }
}

