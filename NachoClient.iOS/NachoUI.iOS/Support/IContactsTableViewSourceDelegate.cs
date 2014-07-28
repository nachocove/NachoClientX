//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface IContactsTableViewSourceDelegate
    {
        void ContactSelectedCallback(McContact contact);
        void PerformSegueForDelegate (string identifier, NSObject sender);
    }
}

