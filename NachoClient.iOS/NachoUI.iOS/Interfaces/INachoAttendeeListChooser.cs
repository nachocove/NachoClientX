//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoAttendeeListChooser
    {
        void Setup (INachoAttendeeListChooserDelegate owner, List<McAttendee> attendees, McAbstrCalendarRoot c, bool editing, bool organizer);
        void DismissViewController (bool animated, Action action);
    }

    public interface INachoAttendeeListChooserDelegate
    {
        void UpdateAttendeeList (List<McAttendee> attendees);
        void DismissINachoAttendeeListChooser (INachoAttendeeListChooser vc);
    }
}

