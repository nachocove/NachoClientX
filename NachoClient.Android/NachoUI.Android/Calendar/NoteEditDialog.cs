﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
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

namespace NachoClient.AndroidClient
{
    public class NoteEditDialog : NcDialogFragment
    {
        private string Value;
        private Action<string> SaveAction;
        private TextView TextField;

        public NoteEditDialog (string value, Action<string> saveAction) : base ()
        {
            Value = value;
            SaveAction = saveAction;
            RetainInstance = true;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            var view = Activity.LayoutInflater.Inflate (Resource.Layout.NoteEditDialog, null);
            TextField = view.FindViewById (Resource.Id.text_entry) as TextView;
            TextField.Text = Value;
            builder.SetView (view);
            builder.SetNegativeButton (Resource.String.text_dialog_cancel, CancelClicked);
            builder.SetPositiveButton (Resource.String.text_dialog_save, SaveClicked);
            return builder.Create ();
        }

        public override void OnDestroy ()
        {
            base.OnDestroy ();
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

        public override void OnResume ()
        {
            base.OnResume ();
            Dialog.Window.SetSoftInputMode (SoftInput.StateAlwaysVisible);
        }

        void SaveClicked (object sender, DialogClickEventArgs e)
        {
            SaveAction (TextField.Text);
            Dismiss ();
        }

        void CancelClicked (object sender, DialogClickEventArgs e)
        {
            Dismiss ();
        }

        public override void OnDismiss (IDialogInterface dialog)
        {
            SaveAction = null;
            TextField = null;
            base.OnDismiss (dialog);
        }
    }
}
