//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;

namespace NachoClient.iOS
{
    public interface INachoMessageController
    {
        void SetOwner (INachoMessageControllerDelegate o);
        void DismissViewController (bool animated, NSAction action);
    }

    public interface INachoMessageControllerDelegate
    {
        void DismissMessageViewController (INachoMessageController vc);
    }
}

