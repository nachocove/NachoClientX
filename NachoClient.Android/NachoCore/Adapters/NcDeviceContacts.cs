//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoCore
{
    public class NcDeviceContacts
    {
        public static void Run ()
        {
            NcTask.Run (Process, "NcDeviceContacts");
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
                ProcessContacts ();
            } finally {
                running = false;
            }
        }

        private static void ProcessContacts ()
        {
            var folder = McFolder.GetDeviceContactsFolder ();
            NcAssert.NotNull (folder);
            var deviceContacts = Contacts.Instance.GetContacts ();
            if (null == deviceContacts) {
                return;
            }
            Func<McContact, McContact> inserter = (contact) => {
                NcModel.Instance.RunInTransaction (() => {
                    contact.Insert ();
                    folder.Link (contact);
                });
                return contact;
            };
            Func<McContact, McContact> updater = (contact) => {
                NcModel.Instance.RunInTransaction (() => {
                    contact.Update ();
                });
                return contact;
            };
            List<McMapFolderFolderEntry> present = McMapFolderFolderEntry.QueryByFolderIdClassCode (folder.AccountId, folder.Id, 
                                                       McAbstrFolderEntry.ClassCodeEnum.Contact);
            int insertCount = 0, updateCount = 0;
            foreach (var deviceContact in deviceContacts) {
                // Use the TPL like iOS GCD here. Schedule chunks.
                var task = NcTask.Run (() => {
                    var existing = McContact.QueryByDeviceUniqueId (deviceContact.UniqueId);
                    if (null != existing) {
                        var removed = present.RemoveAll (x => x.FolderEntryId == existing.Id);
                        NcAssert.AreEqual (1, removed);
                        if (deviceContact.LastUpdate <= existing.DeviceLastUpdate) {
                            return;
                        }
                    }
                    NcResult result;
                    try {
                        result = deviceContact.ToMcContact (existing);
                    } catch (Exception ex) {
                        Log.Error (Log.LOG_SYS, "Exception during ToMcContact: {0}", ex.ToString ());
                        return;
                    }
                    if (!result.isOK ()) {
                        Log.Error (Log.LOG_SYS, "Failed to create McContact from device contact {0}", deviceContact.UniqueId);
                        return;
                    }
                    var contact = result.GetValue<McContact> ();
                    if (null == existing) {
                        inserter.Invoke (contact);
                        ++ insertCount;
                    } else {
                        updater.Invoke (contact);
                        ++ updateCount;
                    }
                }, "NcDeviceContacts:Process", true);
                task.Wait (NcTask.Cts.Token);
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            }
            Log.Info (Log.LOG_SYS, "NcDeviceContacts: {0} inserted, {1} updated, cleaning up {2} dead links.", 
                insertCount, updateCount, present.Count);
            // If it isn't in the list of device contacts, it needs to be removed.
            foreach (var map in present) {
                // Use the TPL like iOS GCD here. Schedule chunks.
                var task = NcTask.Run (() => {
                    NcModel.Instance.RunInTransaction (() => {
                        folder.Unlink (map.FolderEntryId, McAbstrFolderEntry.ClassCodeEnum.Contact);
                        McContact.DeleteById<McContact> (map.FolderEntryId);
                    });
                }, "NcDeviceContacts:Delete", true);
                task.Wait (NcTask.Cts.Token);
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            }
        }
    }
}

