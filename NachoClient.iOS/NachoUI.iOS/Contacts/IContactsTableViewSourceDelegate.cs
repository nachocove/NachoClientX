//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface IContactsTableViewSourceDelegate
    {
        void ContactSelectedCallback (McContact contact);
        void EmailSwipeHandler (McContact contact);
        void CallSwipeHandler (McContact contact);
    }
}

