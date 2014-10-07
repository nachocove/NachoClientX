//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public enum DateControllerType {
        Defer,
        Intent,
    }

    public interface INachoDateController
    {
        void SetOwner (INachoDateControllerParent o);
        void SetDateControllerType (DateControllerType type);
        void DimissDateController (bool animated, NSAction action);
    }

    public interface INachoDateControllerParent
    {
        void DateSelected (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate);
        void DismissChildDateController (INachoDateController vc);
    }
}