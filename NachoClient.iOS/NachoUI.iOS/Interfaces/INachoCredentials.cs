//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{

    public enum NachoCredentialsRequestEnum
    {
        InitialAsk,
        CredReqCallback,
        ServerConfCallback,
    };

    public interface INachoCredentials
    {
        void Setup (INachoCredentialsDelegate owner, McAccount.AccountServiceEnum service, NachoCredentialsRequestEnum why, string email, string password);
    }

    public interface INachoCredentialsDelegate
    {
        void CredentialsDismissed (UIViewController vc, bool startInAdvanced, string email, string password, NachoCredentialsRequestEnum why, bool startOver);
    }
}

