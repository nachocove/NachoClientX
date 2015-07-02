//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{

    public interface INachoCredentials
    {
        void Setup (INachoCredentialsDelegate owner, McAccount.AccountServiceEnum service, bool credReqCallback, string email, string password);
    }

    public interface INachoCredentialsDelegate
    {
        void CredentialsDismissed(UIViewController vc, bool startInAdvanced, string email, string password, bool credReqCallback, bool startOver);
    }
}

