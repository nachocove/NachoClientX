//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Prompt = NachoCore.Utils.LoginProtocolControl.Prompt;

namespace NachoCore
{
    public interface ILoginProtocol
    {
        void FinishUp ();

        void PromptForService ();

        void ShowAdvancedConfiguration (Prompt prompt);

        void ShowNoNetwork ();

        void Start ();

        void StartOver ();

        void UpdateUI ();

        void PromptForCredentials ();

        void StartGoogleLogin ();

        void StartSync ();

        void ShowSupport ();

        void ShowTutorial ();

        void Done ();

        void ShowCertAsk ();

        void Quit ();

        void ShowCredReq ();

        void ShowWaitingScreen (string waitingMessage);

        void ShowDuplicateAccount ();

        void ShowCertRejected ();

        void ShowServerConfCallback ();
    }
}

