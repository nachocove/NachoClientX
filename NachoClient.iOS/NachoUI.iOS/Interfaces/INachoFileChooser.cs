//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Model;
using UIKit;

namespace NachoClient.iOS
{
    public interface INachoFileChooser
    {
        void SetOwner (INachoFileChooserParent o, McAccount account);

        void DismissFileChooser (bool animated, Action action);
    }

    public interface INachoFileChooserParent
    {
        void SelectFile (INachoFileChooser vc, McAbstrObject file);

        void Append (McAttachment attachment);

        void AttachmentUpdated (McAttachment attachment);

        void DismissPhotoPicker ();

        void PresentFileChooserViewController (UIViewController vc);
    }
}

