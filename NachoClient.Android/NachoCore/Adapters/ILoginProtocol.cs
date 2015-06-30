//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore
{
    public interface ILoginProtocol
    {
        void FinishUp ();

        void PromptForService ();

        void ShowAdvancedConfiguration ();

        void ShowAdvancedConfigurationWithError ();

        void ShowNoNetwork();

        void Start ();

        void StartOver ();

        void UpdateUI ();

        void PromptForCredentials ();

        void StartGoogleLogin ();

        void StartGoogleLoginWithComplaint ();

        void StartSync ();

        void TryAgainOrQuit ();

        void ShowTutorial ();

        void Done ();

        void ShowCertAsk ();

        void Quit ();

        void ShowCredReq ();
    }
}

