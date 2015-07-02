//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoFileChooser
    {
        void SetOwner (INachoFileChooserParent o);

        void DismissFileChooser (bool animated, Action action);
    }

    public interface INachoFileChooserParent
    {
        void SelectFile (INachoFileChooser vc, McAbstrObject file);

        void DismissChildFileChooser (INachoFileChooser vc);

        void Append (McAttachment attachment);

        void DismissPhotoPicker ();
    }
}

