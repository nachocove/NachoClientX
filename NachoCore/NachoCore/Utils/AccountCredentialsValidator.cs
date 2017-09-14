//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;

using NachoCore.Model;

namespace NachoCore.Utils
{
    public class AccountCredentialsValidator
    {

        McAccount Account;

        #region Creating a Validator

        public AccountCredentialsValidator (McAccount account)
        {
            Account = account;
        }

        #endregion

        #region Validation Process

        public void Validate (string password, Action<bool> completion)
        {
            Completion = completion;
            StartListeningForStatusInd ();
            var creds = Account.GetCred ();
            ServersNeedingValidation = Account.GetServers ();
            TestCreds = new McCred ();
            TestCreds.Username = creds.Username;
            TestCreds.UserSpecifiedUsername = creds.UserSpecifiedUsername;
            TestCreds.SetTestPassword (password);
            ValidateNextServer ();
        }

        public void Stop ()
        {
            Completion = null;
            StopListeningForStatusInd ();
        }

        #endregion

        #region Private Helpers

        Action<bool> Completion;
        List<McServer> ServersNeedingValidation;
        McCred TestCreds;

        void ValidateNextServer ()
        {
            if (ServersNeedingValidation == null) {
                return;
            }
            if (ServersNeedingValidation.Count == 0) {
                CompleteValidation (success: true);
            } else {
                var server = ServersNeedingValidation.First ();
                ServersNeedingValidation.RemoveAt (0);
                if (!BackEnd.Instance.ValidateConfig (Account.Id, server, TestCreds).isOK ()) {
                    FailValidation ();
                }
            }

        }

        void CompleteValidation (bool success)
        {
            StopListeningForStatusInd ();
            if (success) {
            }
            var completion = Completion;
            Completion = null;
            completion?.Invoke (success);
        }

        void FailValidation ()
        {
            ServersNeedingValidation = null;
            TestCreds = null;
            CompleteValidation (success: false);
        }

        void SavePassword ()
        {
            var creds = Account.GetCred ();
            creds.UpdatePassword (TestCreds.GetTestPassword ());
            creds.Update ();
            BackEnd.Instance.CredResp (Account.Id);
        }

        #endregion

        #region Event Listener

        bool IsListeningForStatusInd;

        void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent += StatusIndCallback;
                IsListeningForStatusInd = true;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent -= StatusIndCallback;
                IsListeningForStatusInd = false;
            }
        }

        void StatusIndCallback (object sender, EventArgs e)
        {
            var statusEvent = e as StatusIndEventArgs;
            if (statusEvent.Account == null || statusEvent.Account.Id != Account.Id) {
                return;
            }
            switch (statusEvent.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ValidateConfigSucceeded:
                ValidateNextServer ();
                break;
            case NcResult.SubKindEnum.Error_ValidateConfigFailedAuth:
            case NcResult.SubKindEnum.Error_ValidateConfigFailedComm:
            case NcResult.SubKindEnum.Error_ValidateConfigFailedUser:
                FailValidation ();
                break;
            }
        }

        #endregion

        ~AccountCredentialsValidator ()
        {
            Stop ();
        }
    }
}
