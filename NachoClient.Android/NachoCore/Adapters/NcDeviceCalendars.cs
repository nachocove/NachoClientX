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
        private int InsertCount = 0, UpdateCount = 0, PresentCount = 0;

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
            if (!DeviceCalendars.MoveNext ()) {
                return true;
            }
            var deviceCalendar = DeviceCalendars.Current;
            // defensive.
            if (null == deviceCalendar) {
                return true;
            }
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
                    Log.Error (Log.LOG_SYS, "Failed to create McCalendar from device calendar {0}", deviceCalendar.ServerId);
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
                if (1 != count) {
                    Log.Error (Log.LOG_SYS, "RemoveAll found {0} for {1}/{2}", count, deviceCalendar.ServerId, existing.Id);
                }
                // If present and stale, update it.
                if (default(DateTime) == deviceCalendar.LastUpdate ||
                    default(DateTime) == existing.DeviceLastUpdate ||
                    deviceCalendar.LastUpdate > existing.DeviceLastUpdate)
                {
                    NcModel.Instance.RunInTransaction (() => {
                        Folder.Unlink (existing);
                        existing.Delete ();
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
                PresentCount = Present.Count;
            }
            if (!Stale.MoveNext ()) {
                return true;
            }
            var map = Stale.Current;
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
                McCalendar.DeleteById<McCalendar> (map.FolderEntryId);
            });
            return false;
        }

        public void Report ()
        {
            NcApplication.Instance.InvokeStatusIndEventInfo (McAccount.GetDeviceAccount (), NcResult.SubKindEnum.Info_CalendarSetChanged);
            Log.Info (Log.LOG_SYS, "NcDeviceCalendars: {0} inserted, {1} updated, cleaning up {2} dead links.", 
                InsertCount, UpdateCount, PresentCount);
        }
    }
}

