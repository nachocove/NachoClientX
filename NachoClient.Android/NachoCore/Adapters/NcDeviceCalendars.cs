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
        public static void Run ()
        {
            NcTask.Run (Process, "NcDeviceCalendars");
        }

        static bool running;
        static object lockObject = new object ();

        private static void Process ()
        {
            lock (lockObject) {
                if (running) {
                    return;
                }
                running = true;
            }
            try {
                ProcessCalendars ();
            } finally {
                running = false;
            }
        }

        private static void ProcessCalendars ()
        {
            var folder = McFolder.GetDeviceCalendarsFolder ();
            NcAssert.NotNull (folder);
            var deviceCalendars = Calendars.Instance.GetCalendars ();
            if (null == deviceCalendars) {
                return;
            }
            Func<PlatformCalendarRecord, McCalendar> inserter = (deviceCalendar) => {
                NcResult result;
                try {
                    result = deviceCalendar.ToMcCalendar ();
                } catch (Exception ex) {
                    Log.Error (Log.LOG_SYS, "Exception during ToMcCalendar: {0}", ex.ToString ());
                    return null;
                }
                if (result.isOK ()) {
                    var calendar = result.GetValue<McCalendar> ();
                    NcAssert.NotNull (calendar);
                    NcModel.Instance.RunInTransaction (() => {
                        calendar.Insert ();
                        folder.Link (calendar);
                    });
                    return calendar;
                } else {
                    Log.Error (Log.LOG_SYS, "Failed to create McCalendar from device calendar {0}", deviceCalendar.UniqueId);
                    return null;
                }
            };
            List<McMapFolderFolderEntry> present = McMapFolderFolderEntry.QueryByFolderIdClassCode (folder.AccountId, folder.Id, 
                                                       McAbstrFolderEntry.ClassCodeEnum.Calendar);
            foreach (var deviceCalendar in deviceCalendars) {
                // Use the TPL like iOS GCD here. Schedule chunks.
                var task = NcTask.Run (() => {
                    var existing = McCalendar.QueryByDeviceUniqueId (deviceCalendar.UniqueId);
                    if (null == existing) {
                        // If missing, insert it.
                        inserter.Invoke (deviceCalendar);
                    } else {
                        var count = present.RemoveAll (x => x.FolderEntryId == existing.Id);
                        if (1 != count) {
                            Log.Error (Log.LOG_SYS, "RemoveAll found {0} for {1}/{2}", count, deviceCalendar.UniqueId, existing.Id);
                        }
                        // If present and stale, update it.
                        if (null == deviceCalendar.LastUpdate || 
                            null == existing.DeviceLastUpdate ||
                            deviceCalendar.LastUpdate > existing.DeviceLastUpdate) {
                            NcModel.Instance.RunInTransaction (() => {
                                if (null != inserter.Invoke (deviceCalendar)) {
                                    folder.Unlink (existing);
                                    existing.Delete ();
                                }
                            });
                        }
                    }
                    NcApplication.Instance.InvokeStatusIndEventInfo (McAccount.GetDeviceAccount (), NcResult.SubKindEnum.Info_CalendarSetChanged);
                }, "NcDeviceCalendars:Process", true);
                task.Wait (NcTask.Cts.Token);
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            }
            // If it isn't in the list of device calendars, it needs to be removed.
            foreach (var map in present) {
                // Use the TPL like iOS GCD here. Schedule chunks.
                var task = NcTask.Run (() => {
                    NcModel.Instance.RunInTransaction (() => {
                        folder.Unlink (map.FolderEntryId, McAbstrFolderEntry.ClassCodeEnum.Calendar);
                        McCalendar.DeleteById<McCalendar> (map.FolderEntryId);
                    });
                    NcApplication.Instance.InvokeStatusIndEventInfo (McAccount.GetDeviceAccount (), NcResult.SubKindEnum.Info_CalendarSetChanged);
                }, "NcDeviceCalendars:Delete", true);
                task.Wait (NcTask.Cts.Token);
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            }
        }
    }
}

