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
        void Setup (INachoAttendeeListChooserDelegate owner, McAccount account, IList<McAttendee> attendees,
            McAbstrCalendarRoot c, bool editing, bool organizer, bool recurring);
        void DismissViewController (bool animated, Action action);
    }

    public interface INachoAttendeeListChooserDelegate
    {
        void UpdateAttendeeList (IList<McAttendee> attendees);
        void DismissINachoAttendeeListChooser (INachoAttendeeListChooser vc);
    }
}

