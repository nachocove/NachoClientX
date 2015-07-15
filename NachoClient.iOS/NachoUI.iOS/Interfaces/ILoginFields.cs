//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

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

        void Layout (nfloat newHeight);

        void Validated (McCred verifiedCred, List<McServer> verifiedServers);
    }
}

