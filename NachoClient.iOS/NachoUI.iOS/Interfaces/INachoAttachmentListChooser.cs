//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoAttachmentListChooser
    {
        void SetOwner (INachoAttachmentListChooserDelegate owner, List<McAttachment> attendees, McAbstrCalendarRoot c, bool editing);
        void DismissViewController (bool animated, NSAction action);
    }

    public interface INachoAttachmentListChooserDelegate
    {
        void UpdateAttachmentList (List<McAttachment> attachment);
        void DismissINachoAttachmentListChooser (INachoAttachmentListChooser vc);
    }
}

