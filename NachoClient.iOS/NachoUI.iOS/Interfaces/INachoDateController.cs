//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Model;
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public interface INachoDateController
    {
        void Setup (INachoDateControllerParent owner, McEmailMessageThread thread, NcMessageDeferral.MessageDateType dateControllerType);

        void SetIntentSelector (IntentSelectionViewController selector);

        void DismissDateController (bool animated, Action action);
    }

    public interface INachoDateControllerParent
    {
        void DateSelected (NcMessageDeferral.MessageDateType type, MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate);

        void DismissChildDateController (INachoDateController vc);
    }
}