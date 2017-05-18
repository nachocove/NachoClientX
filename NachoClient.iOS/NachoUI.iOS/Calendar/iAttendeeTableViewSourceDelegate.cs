//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface IAttendeeTableViewSourceDelegate
    {
        void ContactSelectedCallback(McContact contact);
        void SendAttendeeInvite (McAttendee attendee);
        void UpdateLists ();
        void ConfigureAttendeeTable();
        void RemoveAttendee(McAttendee attendee);
        void SyncRequest ();
        Int64 GetAccountId ();
        void EmailSwipeHandler (McContact contact);
        void CallSwipeHandler (McContact contact);
    }
}

