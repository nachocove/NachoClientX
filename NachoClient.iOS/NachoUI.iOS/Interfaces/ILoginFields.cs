//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface ILoginFields
    {
        bool showAdvanced { 
            get; 
            set; 
        }

        UIView View {
            get;
        }

        void Layout ();
    }
}

