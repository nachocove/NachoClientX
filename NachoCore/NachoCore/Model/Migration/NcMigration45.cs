//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Model
{
    /// <summary>
    /// Support the proper synching of multiple device calendars.  This requires two changes to existing objects.
    /// (1) The device account should have a name, because that name is displayed in the calendar field of the event
    /// detail view.  (2) The folder where device events have been kept now needs to be hidden, so it doesn't show
    /// up in the list of calendars that the user can select. (The folder will remain, and will be used as a backstop
    /// if there are problems synching the real device calendars. But it should remain invisible and mostly unused.)
    /// </summary>
    public class NcMigration45 : NcMigration
    {
        public override int GetNumberOfObjects ()
        {
            return 2;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            var deviceAccount = McAccount.GetDeviceAccount ();
            if (null != deviceAccount) {
                deviceAccount.DisplayName = "Device";
                deviceAccount.Update ();
            }
            UpdateProgress (1);

            var deviceCalFolder = McFolder.GetDeviceCalendarsFolder ();
            if (null != deviceCalFolder && deviceCalFolder.IsClientOwned) {
                deviceCalFolder.UpdateSet_IsHidden (true);
            }
            UpdateProgress (1);
        }
    }
}

