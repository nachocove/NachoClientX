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
        private McFolder Folder;
        private IEnumerator<PlatformContactRecord> DeviceContacts = null;
        private IEnumerator<McMapFolderFolderEntry> Stale = null;
        private List<McMapFolderFolderEntry> Present;
        private int InsertCount = 0, UpdateCount = 0, PresentCount = 0, RemovedCount = 0;

        public NcDeviceContacts ()
        {
            var deviceContacts = NachoPlatform.Contacts.Instance.GetContacts ();
            if (null == deviceContacts) {
                return;
            }
            DeviceContacts = deviceContacts.GetEnumerator ();
            Folder = McFolder.GetDeviceContactsFolder ();
            Present = McMapFolderFolderEntry.QueryByFolderIdClassCode (Folder.AccountId, Folder.Id, 
                McAbstrFolderEntry.ClassCodeEnum.Contact);
        }

        public bool ProcessNextContact ()
        {
            if (null == DeviceContacts) {
                return true;
            }
            if (!DeviceContacts.MoveNext ()) {
                return true;
            }
            var deviceContact = DeviceContacts.Current;
            // defensive.
            if (null == deviceContact) {
                return true;
            }
            Func<McContact, McContact> inserter = (record) => {
                NcModel.Instance.RunInTransaction (() => {
                    record.Insert ();
                    Folder.Link (record);
                });
                return record;
            };
            Func<McContact, McContact> updater = (record) => {
                NcModel.Instance.RunInTransaction (() => {
                    record.Update ();
                });
                return record;
            };

            var existing = McContact.QueryByServerId<McContact> (McAccount.GetDeviceAccount ().Id, deviceContact.ServerId);
            if (null != existing) {
                var removed = Present.RemoveAll (x => x.FolderEntryId == existing.Id);
                NcAssert.AreEqual (1, removed);
                if (deviceContact.LastUpdate <= existing.DeviceLastUpdate) {
                    return false;
                }
            }
            NcResult result;
            try {
                result = deviceContact.ToMcContact (existing);
            } catch (Exception ex) {
                Log.Error (Log.LOG_SYS, "Exception during ToMcContact: {0}", ex.ToString ());
                return false;
            }
            if (!result.isOK ()) {
                Log.Error (Log.LOG_SYS, "Failed to create McContact from device contact {0}", deviceContact.ServerId);
                return false;
            }
            var contact = result.GetValue<McContact> ();
            if (null == existing) {
                inserter.Invoke (contact);
                ++InsertCount;
            } else {
                updater.Invoke (contact);
                ++UpdateCount;
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
            var contact = McContact.QueryById<McContact> (map.FolderEntryId);
            if (null == contact) {
                Log.Error (Log.LOG_SYS, "RemoveNextStale: can't find contact");
            } else {
                if (contact.IsAwaitingCreate) {
                    return false;
                }
            }
            NcModel.Instance.RunInTransaction (() => {
                Folder.Unlink (map.FolderEntryId, McAbstrFolderEntry.ClassCodeEnum.Contact);
                if (null != contact) {
                    contact.Delete ();
                }
            });
            RemovedCount++;
            return false;
        }

        public void Report ()
        {
            if ((0 < InsertCount) || (0 < UpdateCount) || (0 < RemovedCount)) {
                NcApplication.Instance.InvokeStatusIndEventInfo (McAccount.GetDeviceAccount (), NcResult.SubKindEnum.Info_ContactSetChanged);
            }
            Log.Info (Log.LOG_SYS, "NcDeviceContacts: {0} inserted, {1} updated, {2} deleted (cleaning up {2} dead links.)", InsertCount, UpdateCount, RemovedCount, PresentCount);
        }
    }
}
