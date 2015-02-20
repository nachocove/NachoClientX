//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoMessageEditor
    {
        void SetOwner (INachoMessageEditorParent o);
        void DismissMessageEditor (bool animated, Action action);
    }

    public interface INachoMessageEditorParent
    {
        void DismissChildMessageEditor (INachoMessageEditor vc);
        void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread);
        void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread);
    }
}

