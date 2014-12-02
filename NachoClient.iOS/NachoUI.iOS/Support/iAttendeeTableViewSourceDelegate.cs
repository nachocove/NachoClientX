﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface IAttendeeTableViewSourceDelegate
    {
        void ContactSelectedCallback(McContact contact);
        void PerformSegueForDelegate (string identifier, NSObject sender);
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

