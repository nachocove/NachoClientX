﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using NachoCore.Model;
using NachoCore.Brain;
using System.Collections.Generic;

namespace NachoClient.iOS
{
    public interface INachoLabelChooser
    {
        void SetOwner (INachoLabelChooserParent owner);
        void SetLabelList (List<string> labelList);
        void SetSelectedName (string selectedName);
    }

    public interface INachoLabelChooserParent
    {
        void PrepareForDismissal (string selectedName);
    }
}