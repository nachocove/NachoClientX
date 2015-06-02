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

        string emailText { 
            get; 
            set; 
        }

        string passwordText { 
            get; 
            set; 
        }

        string serverText { 
            get; 
            set; 
        }

        string usernameText { 
            get; 
            set; 
        }

        void ClearHighlights ();

        void HighlightEmailError ();

        void HighlightCredentials ();

        void HighlightServerConfError ();

        void HighlightUsernameError ();

        void Layout ();

        void RefreshTheServer (ref AdvancedLoginViewController.AccountSettings theAccount);

        void RefreshUI (AdvancedLoginViewController.AccountSettings theAccount);

        void SaveUserSettings (ref AdvancedLoginViewController.AccountSettings theAccount);

        void MaybeDeleteTheServer ();

        string GetServerConfMessage(AdvancedLoginViewController.AccountSettings theAccount, string messagePrefix);

        AdvancedLoginViewController.LoginStatus CanUserConnect(out string nuance);

        McAccount CreateAccount();

    }
}

