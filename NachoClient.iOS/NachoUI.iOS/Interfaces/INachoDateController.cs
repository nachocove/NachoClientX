﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public enum DateControllerType {
        None,
        Defer,
        Intent,
    }

    public interface INachoDateController
    {
        void Setup (INachoDateControllerParent owner, McEmailMessageThread thread, DateControllerType dateControllerType);
        void SetIntentSelector (IntentSelectionViewController selector);
        void DimissDateController (bool animated, NSAction action);
    }

    public interface INachoDateControllerParent
    {
        void DateSelected (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate);
        void DismissChildDateController (INachoDateController vc);
    }
}