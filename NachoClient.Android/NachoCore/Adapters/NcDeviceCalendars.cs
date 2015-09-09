//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoCore
{
    public class NcDeviceCalendars
    {
        private McFolder Folder;
        private IEnumerator<PlatformCalendarRecord> DeviceCalendars = null;
        private IEnumerator<McMapFolderFolderEntry> Stale = null;
        private List<McMapFolderFolderEntry> Present;
        private int InsertCount = 0, UpdateCount = 0, DeleteCount = 0;
        private int InsertTotal = 0, UpdateTotal = 0, DeleteTotal = 0;

        public NcDeviceCalendars ()
        {
            var deviceCalendars = Calendars.Instance.GetCalendars ();
            if (null == deviceCalendars) {
                return;
            }
            DeviceCalendars = deviceCalendars.GetEnumerator ();
            Folder = McFolder.GetDeviceCalendarsFolder ();
            Present = McMapFolderFolderEntry.QueryByFolderIdClassCode (Folder.AccountId, Folder.Id, 
                McAbstrFolderEntry.ClassCodeEnum.Calendar);
        }

        public bool ProcessNextCal ()
        {
            if (null == DeviceCalendars) {
                return true;
            }

            PlatformCalendarRecord deviceCalendar;
            do {
                if (!DeviceCalendars.MoveNext ()) {
                    return true;
                }
                deviceCalendar = DeviceCalendars.Current;
                // Ignore items that are missing or that don't have an ID.  This can happen if a device calendar item is deleted
                // in between the initial query that finds the events and the processing of the events.
            } while (null == deviceCalendar || string.IsNullOrEmpty (deviceCalendar.ServerId));

            Func<PlatformCalendarRecord, McCalendar> inserter = (record) => {
                NcResult result;
                try {
                    result = record.ToMcCalendar ();
                } catch (Exception ex) {
                    Log.Error (Log.LOG_SYS, "Exception during ToMcCalendar: {0}", ex.ToString ());
                    return null;
                }
                if (result.isOK ()) {
                    var calendar = result.GetValue<McCalendar> ();
                    NcAssert.NotNull (calendar);
                    NcModel.Instance.RunInTransaction (() => {
                        calendar.Insert ();
                        Folder.Link (calendar);
                    });
                    return calendar;
                } else {
                    Log.Error (Log.LOG_SYS, "Failed to create McCalendar from device calendar {0}", record.ServerId);
                    return null;
                }
            };

            var existing = McCalendar.QueryByServerId<McCalendar> (McAccount.GetDeviceAccount ().Id, deviceCalendar.ServerId);
            if (null == existing) {
                // If missing, insert it.
                inserter.Invoke (deviceCalendar);
                ++ InsertCount;
            } else {
                var count = Present.RemoveAll (x => x.FolderEntryId == existing.Id);
                NcAssert.True (2 > count, "Multiple folder entries were found for a single McCalendar");
                if (0 == count) {
                    // Two events have the same ServerId.  This can happen on iOS, even though the iOS
                    // documentation says otherwise.  On iOS, the two events are copies of the same event.
                    // The first event has already been processed, so the best thing to do is to ignore
                    // this one.
                    Log.Info (Log.LOG_SYS, "Two device events have the same server ID: {0}", deviceCalendar.ServerId);
                    return false;
                }
                // If present and stale, update it.
                if (default(DateTime) == deviceCalendar.LastUpdate ||
                    default(DateTime) == existing.DeviceLastUpdate ||
                    deviceCalendar.LastUpdate > existing.DeviceLastUpdate)
                {
                    NcModel.Instance.RunInTransaction (() => {
                        Folder.Unlink (existing);
                        existing.DeleteDeviceItem ();
                        if (null != inserter.Invoke (deviceCalendar)) {
                            ++ UpdateCount;
                        } else {
                            Log.Error (Log.LOG_SYS, "Unable to insert device calendar {0}", deviceCalendar.ServerId);
                        }
                    });
                }
            }
            return false;
        }

        public bool RemoveNextStale ()
        {
            if (null == Stale) {
                if (null == Present) {
                    return true;
                }
                Stale = Present.GetEnumerator ();
            }
            if (!Stale.MoveNext ()) {
                return true;
            }
            var map = Stale.Current;
            ++DeleteCount;
            var cal = McCalendar.QueryById<McCalendar> (map.FolderEntryId);
            if (null == cal) {
                Log.Error (Log.LOG_SYS, "RemoveNextStale: can't find cal");
            } else {
                if (cal.IsAwaitingCreate) {
                    return false;
                }
            }
            NcModel.Instance.RunInTransaction (() => {
                Folder.Unlink (map.FolderEntryId, McAbstrFolderEntry.ClassCodeEnum.Calendar);
                if (null != cal) {
                    cal.DeleteDeviceItem ();
                }
            });
            return false;
        }

        public void Report ()
        {
            if (0 < InsertCount || 0 < UpdateCount || 0 < DeleteCount) {
                NcApplication.Instance.InvokeStatusIndEventInfo (McAccount.GetDeviceAccount (), NcResult.SubKindEnum.Info_CalendarSetChanged);
            }
            // If any McCalendar items were inserted or updated, then an EventSetChanged event will be fired after
            // the CalendarSetChanged event is processed.  But if the only action was deleting McCalendar items, then
            // CalendarSetChanged will not trigger EventSetChanged.  So EventSetChanged needs to be fired explicitly.
            if (0 == InsertCount && 0 == UpdateCount && 0 < DeleteCount) {
                NcApplication.Instance.InvokeStatusIndEventInfo (McAccount.GetDeviceAccount (), NcResult.SubKindEnum.Info_EventSetChanged);
            }
            InsertTotal += InsertCount;
            UpdateTotal += UpdateCount;
            DeleteTotal += DeleteCount;
            Log.Info (Log.LOG_SYS, "NcDeviceCalendars: {0}/{1} inserted, {2}/{3} updated, {4}/{5} deleted. (current round / total so far)", 
                InsertCount, InsertTotal, UpdateCount, UpdateTotal, DeleteCount, DeleteTotal);
            InsertCount = 0;
            UpdateCount = 0;
            DeleteCount = 0;
        }
    }
}

