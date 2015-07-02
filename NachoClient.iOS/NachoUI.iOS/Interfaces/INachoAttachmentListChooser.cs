//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoAttachmentListChooser
    {
        void SetOwner (INachoAttachmentListChooserDelegate owner, List<McAttachment> attendees, McAbstrCalendarRoot c);
        void DismissViewController (bool animated, Action action);
    }

    public interface INachoAttachmentListChooserDelegate
    {
        void UpdateAttachmentList (List<McAttachment> attachment);
        void DismissINachoAttachmentListChooser (INachoAttachmentListChooser vc);
    }
}

