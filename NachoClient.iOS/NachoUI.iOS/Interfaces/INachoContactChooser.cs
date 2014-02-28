//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using NachoCore;


namespace NachoClient.iOS
{
    public interface INachoContactChooser
    {
        void SetOwner (INachoContactChooserDelegate owner, NcEmailAddress address);
        void DismissViewController (bool animated, NSAction action);
    }

    public interface INachoContactChooserDelegate
    {
        void UpdateEmailAddress (NcEmailAddress address);
        void DeleteEmailAddress (NcEmailAddress address);
        void DismissINachoContactChooser (INachoContactChooser vc);
    }
}

