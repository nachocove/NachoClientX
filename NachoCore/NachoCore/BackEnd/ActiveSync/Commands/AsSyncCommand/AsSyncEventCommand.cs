using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using System.IO;

namespace NachoCore.ActiveSync
{
    public partial class AsSyncCommand : AsCommand
    {
        // [MS-ASCMD]
        // When the client sends a Sync command request to the server and
        // a new item has been added to the server collection since the last
        // synchronization, the server responds with an Add element in a
        // Commands element. This Add element specifies the server ID and
        // data of the item to be added to the collection on the client.
        //
        // If the server ID in an Add element from the server matches the
        // server ID for an item on the client, the client treats the
        // addition as a CHANGE to the client item. <<< Key!!!
        //
        // [MS-ASCMD]
        // If a calendar:Exceptions node is not specified, the properties
        // for that calendar:Exceptions node will remain unchanged. If a
        // calendar:Exception node within the calendar:Exceptions node
        // is not present, that particular exception will remain unchanged.
        // If the airsyncbase:Body or airsyncbase:Data elements are not
        // present, the corresponding properties will remain unchanged.
        //
        // In all other cases, if an in-schema property is not specified
        // in a change request, the property is actively deleted from the
        // item on the server.
        //
        // This method is public for unit tests.

        public static void ServerSaysAddOrChangeCalendarItem (int accountId, XElement command, McFolder folder)
        {
            string cmdNameWithAccount = string.Format ("AsSyncCommand({0})", folder.AccountId);
            var xmlServerId = command.Element (Ns + Xml.AirSync.ServerId);
            if (null == xmlServerId || null == xmlServerId.Value || string.Empty == xmlServerId.Value) {
                Log.Error (Log.LOG_AS, "{0}: ServerSaysAddOrChangeCalendarItem: No ServerId present.", cmdNameWithAccount);
                return;
            }
            // If the server attempts to overwrite, delete the pre-existing record first.
            var oldItem = McCalendar.QueryByServerId<McCalendar> (folder.AccountId, xmlServerId.Value);
            if (Xml.AirSync.Add == command.Name.LocalName && null != oldItem) {
                oldItem.Delete ();
                oldItem = null;
            }
            var h = new AsHelpers ();
            McCalendar newItem = null;
            try {
                var r = h.ParseCalendar (accountId, Ns, command);
                newItem = r.GetValue<McCalendar> ();
                NcAssert.True (r.isOK (), "ParseCalendar");
                NcAssert.NotNull (newItem, "newItem");
            } catch (Exception ex) {
                Log.Error (Log.LOG_AS, "{0}: ServerSaysAddOrChangeCalendarItem: Exception parsing: {1}", cmdNameWithAccount, ex.ToString ());
                if (null == newItem || null == newItem.ServerId || string.Empty == newItem.ServerId) {
                    newItem = new McCalendar () {
                        ServerId = xmlServerId.Value,
                    };
                }
                newItem.IsIncomplete = true;
            }
            // Check if the UID and ServerId are consistent.  We have seen cases where the ServerId
            // for an event seems to change over time, but we don't know what triggers it.  It is
            // hoped that these error messages will identify the source of the issue.
            if (string.IsNullOrEmpty (newItem.UID)) {
                if (null != newItem.attendees && 0 < newItem.attendees.Count) {
                    // An appointment without a UID is OK.  A meeting without a UID is a problem.
                    Log.Error (Log.LOG_SYNC, "{0}: ActiveSync command sent a meeting without a UID.", cmdNameWithAccount);
                }
            } else {
                var sameUid = McCalendar.QueryByUID (newItem.AccountId, newItem.UID);
                if (null != sameUid && sameUid.ServerId != newItem.ServerId) {
                    // It is normal for there to be duplicate UIDs for a short period of time when the server
                    // changes an event using an add/delete.  So this is probably not an error.
                    Log.Info (Log.LOG_SYNC, "{0}: Two events have the same UID ({1}) but different ServerId ({2} and {3}). This will likely result in a duplicate event.", cmdNameWithAccount,
                        newItem.UID, sameUid.ServerId, newItem.ServerId);
                }
                if (null != oldItem && oldItem.UID != newItem.UID) {
                    Log.Error (Log.LOG_SYNC, "{0}: The UID for event {1} is changing from {2} to {3}", cmdNameWithAccount,
                        newItem.ServerId, oldItem.UID, newItem.UID);
                }
            }

            // If there is no match, insert the new item.
            if (null == oldItem) {
                newItem.AccountId = folder.AccountId;
                NcModel.Instance.RunInTransaction (() => {
                    int ir = newItem.Insert ();
                    NcAssert.True (0 < ir, "newItem.Insert");
                    folder.Link (newItem);
                });
                return;
            }

            // For a merge, we'll update the new entry following
            // the rules stated in the docs & repeated up above.

            // Pull over the Body
            if (0 == newItem.BodyId) {
                newItem.BodyId = oldItem.BodyId;
            }

            // Overwrite the old item with the new item, being sure to preserve the index
            // and other fields specific to our local database.
            // TODO Encapsulate this behavior of replacing a database object in a method
            // of McAbstrObject.
            newItem.Id = oldItem.Id;
            newItem.AccountId = oldItem.AccountId;
            newItem.CreatedAt = oldItem.CreatedAt;
            newItem.RecurrencesGeneratedUntil = DateTime.MinValue; // Force regeneration of events
            folder.UpdateLink (newItem);
            int ur = newItem.Update ();
            NcAssert.True (0 < ur, "newItem.Update");
        }
    }
}
