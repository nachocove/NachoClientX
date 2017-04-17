//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.AndroidClient
{
    public class PasswordUpdateDialog : NcDialogFragment, View.IOnClickListener
    {

        McAccount Account;
        private EditText UsernameField;
        private EditText PasswordField;

        private Button SaveButton {
            get {
                return (Dialog as AlertDialog).GetButton ((int)DialogButtonType.Positive);
            }
        }

        #region Creating a Dialog

        public PasswordUpdateDialog (McAccount account) : base ()
        {
            Account = account;
        }

        #endregion

        #region Dialog Lifecycle

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            builder.SetTitle (Resource.String.password_update_title);
            var view = Activity.LayoutInflater.Inflate (Resource.Layout.PasswordUpdateDialog, null);
            UsernameField = view.FindViewById (Resource.Id.username) as EditText;
            PasswordField = view.FindViewById (Resource.Id.password) as EditText;
            UsernameField.Text = Account.EmailAddr;
            UsernameField.Enabled = false;
            PasswordField.TextChanged += PasswordTextChanged;
            builder.SetView (view);
            builder.SetNegativeButton (Resource.String.password_update_cancel, CancelClicked);
            builder.SetPositiveButton (Resource.String.password_update_save, SaveClicked);
            builder.SetCancelable (false);
            return builder.Create ();
        }

        public override void OnResume ()
        {
            base.OnResume ();
            PasswordField.RequestFocus ();
            Dialog.Window.SetSoftInputMode (SoftInput.StateAlwaysVisible);
            SaveButton.SetOnClickListener (this);
            UpdateSaveEnabled ();
        }

        public override void OnDismiss (IDialogInterface dialog)
        {
            StopListeningForStatusInd ();
            UsernameField = null;
            PasswordField = null;
            base.OnDismiss (dialog);
        }

        #endregion

        #region User Actions

        public void OnClick (View view)
        {
            if (view == SaveButton) {
                SaveClicked (view, null);
            }
        }

        void PasswordTextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            UpdateSaveEnabled ();
        }

        void SaveClicked (object sender, DialogClickEventArgs e)
        {
            Save ();
        }

        void CancelClicked (object sender, DialogClickEventArgs e)
        {
            Dismiss ();
        }

        #endregion

        #region Private Helpers

        List<McServer> ServersNeedingValidation;
        McCred TestCreds;

        void Save ()
        {
            DisableActions ();
            StartListeningForStatusInd ();
            var creds = Account.GetCred ();
            ServersNeedingValidation = Account.GetServers ();
            TestCreds = new McCred ();
            TestCreds.Username = creds.Username;
            TestCreds.UserSpecifiedUsername = creds.UserSpecifiedUsername;
            TestCreds.SetTestPassword (PasswordField.Text);
            ValidateNextServer ();
        }

        void ValidateNextServer ()
        {
            if (ServersNeedingValidation == null) {
                return;
            }
            if (ServersNeedingValidation.Count == 0) {
                CompleteValidation ();
            } else {
                var server = ServersNeedingValidation.First ();
                ServersNeedingValidation.RemoveAt (0);
                if (!BackEnd.Instance.ValidateConfig (Account.Id, server, TestCreds).isOK ()) {
                    FailValidation ();
                }
            }

        }

        void CompleteValidation ()
        {
            StopListeningForStatusInd ();
            Dismiss ();
        }

        void FailValidation ()
        {
            EnableActions ();
            StopListeningForStatusInd ();
            ServersNeedingValidation = null;
            TestCreds = null;
            PasswordField.Text = "";
            PasswordField.RequestFocus ();
            Dialog.Window.SetSoftInputMode (SoftInput.StateAlwaysVisible);
        }

        void DisableActions ()
        {
            PasswordField.Enabled = false;
            SaveButton.Enabled = false;
        }

        void EnableActions ()
        {
            PasswordField.Enabled = true;
            SaveButton.Enabled = true;
        }

        void UpdateSaveEnabled ()
        {
            SaveButton.Enabled = PasswordField.Text.Length > 0;
        }

        #endregion

        #region Event Listener

        bool IsListeningForStatusInd = false;

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
    }
}
