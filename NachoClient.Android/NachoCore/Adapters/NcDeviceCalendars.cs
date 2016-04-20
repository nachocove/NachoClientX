//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;
using System.Linq;

namespace NachoCore
{
    public class NcDeviceCalendars
    {
        private IEnumerator<PlatformCalendarFolderRecord> deviceFolders = null;
        private IEnumerator<PlatformCalendarRecord> deviceEvents = null;
        private List<McFolder> existingAppFolders = null;
        private List<McMapFolderFolderEntry> existingAppEvents = null;

        private IEnumerator<McMapFolderFolderEntry> staleEvents = null;
        private IEnumerator<McFolder> staleFolders = null;

        // iOS does not always process moves correctly.  When an event is moved from one calendar
        // to another, it sometimes remains in the older calendar.  This results in two events with
        // the same uinque ID.  (This bug happens when the move is done in the iOS Calendar app, not
        // just in Nacho Mail.)  Keep track of the ServerIds that have been processed, and ignore
        // all duplicates.  There is not a good way to figure out which one is the correct event,
        // so the code will just process the first one that it sees.
        private HashSet<string> processedEventServerIds = new HashSet<string> ();

        private int InsertCount = 0, UpdateCount = 0, MoveCount = 0, DeleteCount = 0;
        private int InsertTotal = 0, UpdateTotal = 0, MoveTotal = 0, DeleteTotal = 0;

        public NcDeviceCalendars ()
        {
            IEnumerable<PlatformCalendarFolderRecord> deviceFoldersCollection;
            IEnumerable<PlatformCalendarRecord> deviceEventsCollection;
            Calendars.Instance.GetCalendars (out deviceFoldersCollection, out deviceEventsCollection);
            if (null == deviceEventsCollection || null == deviceFoldersCollection) {
                return;
            }
            deviceFolders = deviceFoldersCollection.GetEnumerator ();
            deviceEvents = deviceEventsCollection.GetEnumerator ();

            existingAppFolders = McFolder.QueryNonHiddenFoldersOfType (McAccount.GetDeviceAccount ().Id, NachoFolders.FilterForCalendars);
            existingAppEvents = new List<McMapFolderFolderEntry> ();
            foreach (var appFolder in existingAppFolders) {
                var events = McMapFolderFolderEntry.QueryByFolderIdClassCode (
                    appFolder.AccountId, appFolder.Id, McAbstrFolderEntry.ClassCodeEnum.Calendar);
                existingAppEvents.AddRange (events);
            }
            // Include events that are in the hidden folder that is used when there is a problem
            // finding the correct device calendar folder.  This is necessary to correctly migrate
            // from when all device events were stored in the same folder.
            var backstopFolder = McFolder.GetDeviceCalendarsFolder ();
            if (null != backstopFolder) {
                var events = McMapFolderFolderEntry.QueryByFolderIdClassCode (
                    backstopFolder.AccountId, backstopFolder.Id, McAbstrFolderEntry.ClassCodeEnum.Calendar);
                existingAppEvents.AddRange (events);
            }
        }

        public bool ProcessNextCalendarFolder ()
        {
            if (null == deviceFolders) {
                return true;
            }
            PlatformCalendarFolderRecord folder;
            do {
                if (!deviceFolders.MoveNext ()) {
                    return true;
                }
                folder = deviceFolders.Current;
            } while (null == folder || string.IsNullOrEmpty (folder.ServerId));

            var existing = McFolder.QueryByServerId (McAccount.GetDeviceAccount ().Id, folder.ServerId);

            if (null == existing) {
                NcResult createMcFolder = null;
                try {
                    createMcFolder = folder.ToMcFolder ();
                } catch (Exception ex) {
                    Log.Error (Log.LOG_SYS, "Exception during ToMcFolder(): {0}", ex.ToString ());
                }
                if (null != createMcFolder) {
                    if (createMcFolder.isOK ()) {
                        var mcFolder = createMcFolder.GetValue<McFolder> ();
                        mcFolder.Insert ();
                    } else {
                        Log.Error (Log.LOG_SYS, "Failed to create McFolder from device calendar folder {0}: {1}", folder.ServerId, createMcFolder.SubKind);
                    }
                }
            } else {
                existingAppFolders.RemoveAll (f => f.Id == existing.Id);
                // The only thing that can change that the app cares about is the display name of the folder
                if (existing.DisplayName != folder.DisplayName) {
                    existing = existing.UpdateWithOCApply<McFolder> ((record) => {
                        ((McFolder)record).DisplayName = folder.DisplayName;
                        return true;
                    });
                }
            }
            return false;
        }

        public bool ProcessNextCalendarEvent ()
        {
            if (null == deviceEvents) {
                return true;
            }

            PlatformCalendarRecord deviceEvent;
            do {
                if (!deviceEvents.MoveNext ()) {
                    return true;
                }
                deviceEvent = deviceEvents.Current;
            } while (null == deviceEvent || string.IsNullOrEmpty (deviceEvent.ServerId) || !processedEventServerIds.Add (deviceEvent.ServerId));

            var existing = McCalendar.QueryByServerId<McCalendar> (McAccount.GetDeviceAccount ().Id, deviceEvent.ServerId);

            if (null == existing) {
                if (InsertMcCalendarFromPlatformEvent (deviceEvent, null, null)) {
                    ++InsertCount;
                }
            } else {
                McFolder existingAppFolder = null;
                var folderEntry = existingAppEvents.Where (x => x.FolderEntryId == existing.Id).FirstOrDefault ();
                if (null == folderEntry) {
                    Log.Error (Log.LOG_SYS, "Existing device calendar item is not in one of the device calendar folders.");
                } else {
                    existingAppEvents.Remove (folderEntry);
                    existingAppFolder = McFolder.QueryById<McFolder> (folderEntry.FolderId);
                }
                if (default(DateTime) == existing.DeviceLastUpdate || default(DateTime) == deviceEvent.LastUpdate || deviceEvent.LastUpdate > existing.DeviceLastUpdate) {
                    // The event has changed.  Delete the existing one and insert a new one.
                    if (InsertMcCalendarFromPlatformEvent (deviceEvent, existing, existingAppFolder)) {
                        ++UpdateCount;
                    }
                } else if (null != existingAppFolder && existingAppFolder.ServerId != deviceEvent.ParentFolder.ServerId) {
                    // The event has been moved to a different calendar/folder.
                    NcModel.Instance.RunInTransaction (() => {
                        existingAppFolder.Unlink (existing);
                        LinkToFolder (existing, deviceEvent.ParentFolder.ServerId);
                    });
                    ++MoveCount;
                }
            }
            return false;
        }

        public bool RemoveNextStaleEvent ()
        {
            if (null == staleEvents) {
                if (null == existingAppEvents) {
                    return true;
                }
                staleEvents = existingAppEvents.GetEnumerator ();
            }

            if (!staleEvents.MoveNext ()) {
                return true;
            }

            var map = staleEvents.Current;
            var appCal = McCalendar.QueryById<McCalendar> (map.FolderEntryId);
            if (null == appCal) {
                Log.Error (Log.LOG_SYS, "RemoveNextStaleEvent: Can't find calendar item {0}", map.FolderEntryId);
            } else if (!appCal.IsAwaitingCreate) {
                NcModel.Instance.RunInTransaction (() => {
                    var appFolder = McFolder.QueryById<McFolder> (map.FolderId);
                    if (null != appFolder) {
                        appFolder.Unlink (appCal);
                    }
                    appCal.DeleteDeviceItem ();
                });
                ++DeleteCount;
            }
            return false;
        }

        public bool RemoveNextStaleFolder ()
        {
            if (null == staleFolders) {
                if (null == existingAppFolders) {
                    return true;
                }
                staleFolders = existingAppFolders.GetEnumerator ();
            }

            if (!staleFolders.MoveNext ()) {
                return true;
            }

            var folder = staleFolders.Current;
            folder.Delete ();
            return false;
        }

        public void Report ()
        {
            if (0 < InsertCount || 0 < UpdateCount || 0 < DeleteCount) {
                NcApplication.Instance.InvokeStatusIndEventInfo (McAccount.GetDeviceAccount (), NcResult.SubKindEnum.Info_CalendarSetChanged);
            }
            // If any McCalendar items were inserted or updated, then an EventSetChanged event will be fired after
            // the CalendarSetChanged event is processed.  But if the only action was deleting McCalendar items or
            // moving them to a new folder, then CalendarSetChanged will not trigger EventSetChanged.  So
            // EventSetChanged needs to be fired explicitly.
            if (0 == InsertCount && 0 == UpdateCount && (0 < DeleteCount || 0 < MoveCount)) {
                NcApplication.Instance.InvokeStatusIndEventInfo (McAccount.GetDeviceAccount (), NcResult.SubKindEnum.Info_EventSetChanged);
            }
            InsertTotal += InsertCount;
            UpdateTotal += UpdateCount;
            MoveTotal += MoveCount;
            DeleteTotal += DeleteCount;
            Log.Info (Log.LOG_SYS, "NcDeviceCalendars: {0}/{1} inserted, {2}/{3} updated, {4}/{5} moved, {6}/{7} deleted. (current round / total so far)",
                InsertCount, InsertTotal, UpdateCount, UpdateTotal, MoveCount, MoveTotal, DeleteCount, DeleteTotal);
            InsertCount = 0;
            UpdateCount = 0;
            MoveCount = 0;
            DeleteCount = 0;
        }

        private void LinkToFolder (McCalendar calendarItem, string folderServerId)
        {
            var folder = McFolder.QueryByServerId (calendarItem.AccountId, folderServerId);
            if (null == folder) {
                Log.Error (Log.LOG_SYS, "Folder {0} for calendar item {1} was not found. Using the backstop folder instead.",
                    folderServerId, calendarItem.ServerId);
                folder = McFolder.GetDeviceCalendarsFolder ();
            }
            folder.Link (calendarItem);
        }

        private bool InsertMcCalendarFromPlatformEvent (PlatformCalendarRecord deviceEvent, McCalendar existingAppEvent, McFolder existingAppFolder)
        {
            NcResult createEvent;
            try {
                createEvent = deviceEvent.ToMcCalendar ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "Exception during ToMcCalendar for event {0}: {1}", deviceEvent.ServerId, ex.ToString ());
                return false;
            }

            if (createEvent.isOK ()) {
                var appCal = createEvent.GetValue<McCalendar> ();
                NcModel.Instance.RunInTransaction (() => {
                    if (null != existingAppEvent) {
                        if (null != existingAppFolder) {
                            existingAppFolder.Unlink (existingAppEvent);
                        }
                        existingAppEvent.DeleteDeviceItem ();
                    }
                    appCal.Insert ();
                    LinkToFolder (appCal, deviceEvent.ParentFolder.ServerId);
                });
            } else {
                Log.Error (Log.LOG_SYS, "Failed to create McCalendar from device calendar item {0}: {1}", deviceEvent.ServerId, createEvent.SubKind);
            }
            return createEvent.isOK ();
        }
    }
}

