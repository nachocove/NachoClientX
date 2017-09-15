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
        AccountCredentialsValidator Validator;

        private Button SaveButton {
            get {
                return (Dialog as AlertDialog).GetButton ((int)DialogButtonType.Positive);
            }
        }

        #region Creating a Dialog

        public PasswordUpdateDialog (McAccount account) : base ()
        {
            Account = account;
            RetainInstance = true;
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
            Validator?.Stop ();
            Validator = null;
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
        }

        #endregion

        #region Private Helpers

        void Save ()
        {
            DisableActions ();
            Validator = new AccountCredentialsValidator (Account);
            Validator.Validate (PasswordField.Text, (success) => {
                if (success) {
                    CompleteValidation ();
                } else {
                    FailValidation ();
                }
            });
        }

        void CompleteValidation ()
        {
            Dismiss ();
        }

        void FailValidation ()
        {
            EnableActions ();
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

    }
}
