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

        /// McEmailAddress index for organizer email
        [Indexed]
        public int OrganizerEmailAddressId { get; set; }

        /// Unique 300 digit hexidecimal ID generated by the client. Calendar only.
        [MaxLength (300)]
        public string UID { get; set; }

        /// Recurrences are generated into the McEvent table thru this date.
        public DateTime RecurrencesGeneratedUntil { get; set; }

        public override ClassCodeEnum GetClassCode ()
        {
            return McAbstrFolderEntry.ClassCodeEnum.Calendar;
        }

        private List<McException> dbExceptions = null;
        private IList<McException> appExceptions = null;

        [Ignore]
        public IList<McException> exceptions {
            get {
                return GetAncillaryCollection (appExceptions, ref dbExceptions, ReadDbExceptions);
            }
            set {
                NcAssert.NotNull (value, "To clean the exceptions, use an empty list instead of null.");
                appExceptions = value;
            }
        }

        private List<McException> ReadDbExceptions ()
        {
            return McException.QueryExceptionsForCalendarItem (this.Id).ToList ();
        }

        private void DeleteDbExceptions ()
        {
            DeleteAncillaryCollection (ref dbExceptions, ReadDbExceptions);
        }

        private void SaveExceptions ()
        {
            SaveAncillaryCollection (ref appExceptions, ref dbExceptions, ReadDbExceptions, (McException exception) => {
                exception.CalendarId = this.Id;
            }, (McException exception) => {
                return exception.CalendarId == this.Id;
            });
        }

        private void InsertExceptions ()
        {
            InsertAncillaryCollection (ref appExceptions, ref dbExceptions, (McException exception) => {
                exception.CalendarId = this.Id;
            });
        }

        private List<McRecurrence> dbRecurrences = null;
        private IList<McRecurrence> appRecurrences = null;

        [Ignore]
        public IList<McRecurrence> recurrences {
            get {
                return GetAncillaryCollection (appRecurrences, ref dbRecurrences, ReadDbRecurrences);
            }
            set {
                NcAssert.NotNull (value, "To clear the recurrences, use an empty list instead of null.");
                appRecurrences = value;
            }
        }

        private List<McRecurrence> ReadDbRecurrences ()
        {
            return NcModel.Instance.Db.Table<McRecurrence> ()
                .Where (x => x.CalendarId == this.Id).ToList ();
        }

        private void DeleteDbRecurrences ()
        {
            DeleteAncillaryCollection (ref dbRecurrences, ReadDbRecurrences);
        }

        private void SaveRecurrences ()
        {
            SaveAncillaryCollection (ref appRecurrences, ref dbRecurrences, ReadDbRecurrences, (McRecurrence recurrence) => {
                recurrence.CalendarId = this.Id;
            }, (McRecurrence recurrence) => {
                return recurrence.CalendarId == this.Id;
            });
        }

        private void InsertRecurrences ()
        {
            InsertAncillaryCollection (ref appRecurrences, ref dbRecurrences, (McRecurrence recurrence) => {
                recurrence.CalendarId = this.Id;
            });
        }

        private void InsertAddressMap ()
        {
            var map = CreateAddressMap ();
            map.EmailAddressId = OrganizerEmailAddressId;
            map.AddressType = NcEmailAddress.Kind.Organizer;
            map.Insert ();
        }

        private void DeleteAddressMap ()
        {
            McMapEmailAddressEntry.DeleteMapEntries (AccountId, Id, NcEmailAddress.Kind.Organizer);
        }

        public override int Insert ()
        {
            int retval = 0;
            NcModel.Instance.RunInTransaction (() => {
                OrganizerEmailAddressId = McEmailAddress.Get (AccountId, OrganizerEmail);
                retval = base.Insert ();
                InsertExceptions ();
                InsertRecurrences ();
                InsertAddressMap ();
            });
            return retval;
        }

        public override int Update ()
        {
            int retval = 0;
            NcModel.Instance.RunInTransaction (() => {
                OrganizerEmailAddressId = McEmailAddress.Get (AccountId, OrganizerEmail);
                DeleteAddressMap ();
                InsertAddressMap ();
                retval = base.Update ();
                SaveExceptions ();
                SaveRecurrences ();
            });
            return retval;
        }

        public static McCalendar QueryByDeviceUniqueId (string deviceUniqueId)
        {
            var account = McAccount.GetDeviceAccount ();
            return NcModel.Instance.Db.Table<McCalendar> ().Where (x => 
                x.DeviceUniqueId == deviceUniqueId &&
            x.AccountId == account.Id
            ).SingleOrDefault ();
        }

        public static McCalendar QueryByUID (int accountId, string UID)
        {
            var sameUid = NcModel.Instance.Db.Table<McCalendar> ().Where (
                              x => x.AccountId == accountId && x.UID == UID
                          );
            if (1 < sameUid.Count ()) {
                // This shouldn't happen.  But we have seen it happen.
                // Log an error message to help us get a grasp on the
                // problem.  But don't crash.
                Log.Error (Log.LOG_CALENDAR, "There are {0} events with the same UID ({1}). The one with the most recent modification time will be used.",
                    sameUid.Count (), UID);
                McCalendar mostRecent = null;
                foreach (var cal in sameUid) {
                    if (null == mostRecent || cal.LastModified.CompareTo (mostRecent.LastModified) > 0) {
                        mostRecent = cal;
                    }
                }
                return mostRecent;
            } else {
                return sameUid.FirstOrDefault ();
            }
        }

        /// <summary>
        /// Find all the calendar items that might need to have events created so that all events would be
        /// accurate up through the given time.
        /// </summary>
        public static List<McCalendar> QueryOutOfDateRecurrences (DateTime generateUntil)
        {
            return NcModel.Instance.Db.Table<McCalendar> ()
                .Where (x => x.RecurrencesGeneratedUntil < generateUntil && x.IsAwaitingDelete == false).ToList ();
        }

        public List<McException> QueryRelatedExceptions ()
        {
            return McException.QueryExceptionsForCalendarItem (this.Id).ToList ();
        }

        public override void DeleteAncillary ()
        {
            base.DeleteAncillary ();
            DeleteDbExceptions ();
            DeleteDbRecurrences ();
            DeleteAddressMap ();
        }

        public override int Delete ()
        {
            // Normally ancillary data is deleted in DeleteAncillary(), not in Delete(), because
            // McAbstrItem.Delete() might delay the actual deletion of the item due a McPending
            // that references it.  But McEvents are treated differently.  We want to delete them
            // right now so they disappear from the calendar, even if the underlying McCalendar
            // might stick around for a while longer.

            int retval = 0;
            List<NcEventIndex> eventIds = null;

            NcModel.Instance.RunInTransaction (() => {
                eventIds = McEvent.QueryEventIdsForCalendarItem (this.Id);
                McEvent.DeleteEventsForCalendarItem (this.Id);
                retval = base.Delete ();
            });

            // Canceling a local notification may require running some code on the UI thread.
            // Even if the UI thread action is invoked asynchronously, the UI thread will take
            // priority and run for a little while, slowing down this thread.  We don't want
            // the slowdown to happen within a database transaction.  So the actual cancelation
            // is delayed until after all the database work is done.  (But it uses the set of
            // event IDs that were gathered while within the transaction, so those are guaranteed
            // to be the correct events.)
            LocalNotificationManager.CancelNotifications (eventIds);

            // The code that manages McEvents will never notice that the events for this
            // item have been deleted.  So the EventSetChanged status needs to be fired
            // explicitly.
            NcApplication.Instance.InvokeStatusIndEvent(new StatusIndEventArgs() {
                Status = NcResult.Info(NcResult.SubKindEnum.Info_EventSetChanged),
                Account = ConstMcAccount.NotAccountSpecific,
                Tokens = new string[] { DateTime.Now.ToString () },
            });

            return retval;
        }
    }
}

