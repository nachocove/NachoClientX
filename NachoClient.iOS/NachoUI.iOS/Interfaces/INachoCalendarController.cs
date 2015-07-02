//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public enum CalendarItemEditorAction
    {
        undefined,
        create,
        edit,
        view,
    };

    public interface INachoCalendarItemEditor
    {
        void SetOwner (INachoCalendarItemEditorParent owner);
        void SetCalendarItem (McEvent item, CalendarItemEditorAction action);
        void DismissCalendarItemEditor (bool animated, Action action);
    }

    public interface INachoCalendarItemEditorParent
    {
        void DismissChildCalendarItemEditor (INachoCalendarItemEditor vc);
    }
}

