//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using NachoCore.Utils;


namespace NachoClient.iOS
{
    public enum NachoContactType : int {
        EmailRequired = 1,
        PhoneNumberRequired = 2,
        All = (EmailRequired | PhoneNumberRequired),
    };

    public interface INachoContactChooser
    {
        void SetOwner (INachoContactChooserDelegate owner, NcEmailAddress address, NachoContactType type);
        void DismissViewController (bool animated, NSAction action);
    }

    public interface INachoContactChooserDelegate
    {
        void UpdateEmailAddress (NcEmailAddress address);
        void DeleteEmailAddress (NcEmailAddress address);
        void DismissINachoContactChooser (INachoContactChooser vc);
    }
}

