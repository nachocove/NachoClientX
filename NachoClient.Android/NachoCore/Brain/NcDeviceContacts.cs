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
            var deviceContacts = Contacts.Instance.GetContacts ();
            Func<PlatformContactRecord, McContact> inserter = (deviceContact) => {
                var result = deviceContact.ToMcContact ();
                if (result.isOK ()) {
                    var contact = result.GetValue<McContact> ();
                    contact.Insert ();
                    return contact;
                } else {
                    Log.Error (Log.LOG_SYS, "Failed to create McContact from device contact {0}", deviceContact.UniqueId);
                    return null;
                }
            };
            var folder = McFolder.GetDeviceContactsFolder ();
            List<int> present = McMapFolderFolderEntry.QueryByFolderIdClassCode (ConstMcAccount.NotAccountSpecific.Id, folder.Id, McAbstrFolderEntry.ClassCodeEnum.Calendar);
            foreach (var deviceContact in deviceContacts) {
                // Use the TPL like iOS GCD here. Schedule chunks.
                var task = NcTask.Run (() => {
                    var existing = McContact.QueryByDeviceUniqueId (deviceContact.UniqueId);
                    if (null == existing) {
                        // If missing, insert it.
                        inserter.Invoke (deviceContact);
                    } else {
                        present.Remove (existing.Id);
                        // If present and stale, update it.
                        if (deviceContact.LastUpdate > existing.DeviceLastUpdate) {
                            existing.Delete ();
                            inserter.Invoke (deviceContact);
                        }
                    }
                }, "NcDeviceContacts:Process");
                try {
                    task.Wait (NcTask.Cts.Token);
                } catch (OperationCanceledException) {
                    // Stop processing.
                    return;
                } catch (AggregateException aex) {
                    aex.Handle ((ex) => {
                        return ex is OperationCanceledException;
                    });
                    // Stop processing.
                    return;
                }
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            }
            // If it isn't in the list of device contacts, it needs to be removed.
            foreach (var id in present) {
                // Use the TPL like iOS GCD here. Schedule chunks.
                var task = NcTask.Run (() => {
                    McContact.DeleteById<McContact> (id);
                }, "NcDeviceContacts:Delete");
                try {
                    task.Wait (NcTask.Cts.Token);
                } catch (OperationCanceledException) {
                    // Stop processing.
                    return;
                } catch (AggregateException aex) {
                    aex.Handle ((ex) => {
                        return ex is OperationCanceledException;
                    });
                    // Stop processing.
                    return;
                }
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            }
        }
    }
}

