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
        void Cleanup();
        void SetOwner (INachoContactChooserDelegate owner, NcEmailAddress address, NachoContactType type);
    }

    public interface INachoContactChooserDelegate
    {
        void UpdateEmailAddress (INachoContactChooser vc, NcEmailAddress address);
        void DeleteEmailAddress (INachoContactChooser vc, NcEmailAddress address);
        void DismissINachoContactChooser (INachoContactChooser vc);
    }
}

