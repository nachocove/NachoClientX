//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoCore.Brain
{
    public class NcDeviceContacts
    {
        public static void Run ()
        {
            NcTask.Run (Process, "NcDeviceContacts");
        }

        private static void Process ()
        {
            var folder = McFolder.GetDeviceContactsFolder ();
            NcAssert.NotNull (folder);
            var deviceContacts = Contacts.Instance.GetContacts ();
            if (null == deviceContacts) {
                return;
            }
            Func<PlatformContactRecord, McContact> inserter = (deviceContact) => {
                var result = deviceContact.ToMcContact ();
                if (result.isOK ()) {
                    var contact = result.GetValue<McContact> ();
                    NcModel.Instance.RunInTransaction(() => {
                        contact.Insert ();
                        folder.Link (contact);
                    });
                    return contact;
                } else {
                    Log.Error (Log.LOG_SYS, "Failed to create McContact from device contact {0}", deviceContact.UniqueId);
                    return null;
                }
            };
            List<McMapFolderFolderEntry> present = McMapFolderFolderEntry.QueryByFolderIdClassCode (folder.AccountId, folder.Id, McAbstrFolderEntry.ClassCodeEnum.Contact);
            foreach (var deviceContact in deviceContacts) {
                // Use the TPL like iOS GCD here. Schedule chunks.
                var task = NcTask.Run (() => {
                    var existing = McContact.QueryByDeviceUniqueId (deviceContact.UniqueId);
                    if (null == existing) {
                        // If missing, insert it.
                        inserter.Invoke (deviceContact);
                    } else {
                        NcAssert.True (1 == present.RemoveAll (x => x.FolderEntryId == existing.Id));
                        // If present and stale, update it.
                        if (deviceContact.LastUpdate > existing.DeviceLastUpdate) {
                            NcModel.Instance.RunInTransaction(() => {
                                if (null != inserter.Invoke (deviceContact)) {
                                    folder.Unlink (existing);
                                    existing.Delete ();
                                }
                            });
                        }
                    }
                }, "NcDeviceContacts:Process");
                task.Wait (NcTask.Cts.Token);
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            }
            // If it isn't in the list of device contacts, it needs to be removed.
            foreach (var map in present) {
                // Use the TPL like iOS GCD here. Schedule chunks.
                var task = NcTask.Run (() => {
                    NcModel.Instance.RunInTransaction (() => {
                        folder.Unlink (map.FolderEntryId, McAbstrFolderEntry.ClassCodeEnum.Contact);
                        McContact.DeleteById<McContact> (map.FolderEntryId);
                    });
                }, "NcDeviceContacts:Delete");
                task.Wait (NcTask.Cts.Token);
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            }
        }
    }
}

