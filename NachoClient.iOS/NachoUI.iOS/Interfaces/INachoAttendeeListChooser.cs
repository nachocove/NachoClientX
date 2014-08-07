//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoAttendeeListChooser
    {
        void SetOwner (INachoAttendeeListChooserDelegate owner, List<McAttendee> attendees, McCalendar c, bool editing);
        void DismissViewController (bool animated, NSAction action);
    }

    public interface INachoAttendeeListChooserDelegate
    {
        void UpdateAttendeeList (List<McAttendee> attendees);
        void DismissINachoAttendeeListChooser (INachoAttendeeListChooser vc);
    }
}

