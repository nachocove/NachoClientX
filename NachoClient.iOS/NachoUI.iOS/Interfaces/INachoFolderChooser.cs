//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoFolderChooser
    {
        void SetOwner (INachoFolderChooserParent owner, object cookie);

        void DismissFolderChooser (bool animated, NSAction action);
    }

    public interface INachoFolderChooserParent
    {
        void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie);

        void DismissChildFolderChooser (INachoFolderChooser vc);
    }
}

