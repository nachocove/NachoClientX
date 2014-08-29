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

        public static void ServerSaysAddOrChangeCalendarItem (XElement command, McFolder folder)
        {
            // Convert the event to an NcCalendar
            var h = new AsHelpers ();
            var r = h.ParseCalendar (Ns, command);
            McCalendar newItem = r.GetValue<McCalendar> ();

            NcAssert.True (r.isOK (), "ParseCalendar");
            NcAssert.NotNull (newItem, "newItem");

            // Look up the event by ServerId
            var oldItem = McCalendar.QueryByServerId<McCalendar> (folder.AccountId, newItem.ServerId);

            // If there is no match, insert the new item.
            if (null == oldItem) {
                newItem.AccountId = folder.AccountId;
                int ir = newItem.Insert ();
                NcAssert.True (0 < ir, "newItem.Insert");
                folder.Link (newItem);
                return;
            }

            // For a merge, we'll update the new entry following
            // the rules stated in the docs & repeated up above.

            // Pull over the Body
            if (0 == newItem.BodyId) {
                newItem.BodyId = oldItem.BodyId;
                // FIXME - need to handle this merge in the model.
            }

            // Overwrite the old item with the new item
            // to preserve the index, in
            newItem.Id = oldItem.Id;
            newItem.AccountId = oldItem.AccountId;
            int ur = newItem.Update ();
            NcAssert.True (0 < ur, "newItem.Update");
            CalendarHelper.UpdateRecurrences (newItem);
        }
    }
}
   
