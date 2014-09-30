//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using NachoCore.Model;
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public interface INachoIntentChooser
    {
        void SetOwner (INachoIntentChooserParent owner);
        void DismissIntentChooser (bool animated, NSAction action);
    }

    public interface INachoIntentChooserParent
    {
        void SelectIntent (NcMessageIntent.Intent intent);
    }
}