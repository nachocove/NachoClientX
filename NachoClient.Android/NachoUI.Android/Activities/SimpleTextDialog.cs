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
    public class SimpleTextDialog : NcDialogFragment
    {

        private int TitleResource;
        private int HintResource;
        private string Value;
        private Action<string> SaveAction;
        private EditText TextField;

        public SimpleTextDialog (int titleResource, int hintResource, string value, Action<string> saveAction) : base ()
        {
            TitleResource = titleResource;
            HintResource = hintResource;
            Value = value;
            SaveAction = saveAction;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            builder.SetTitle (TitleResource);
            var view = Activity.LayoutInflater.Inflate (Resource.Layout.SimpleTextDialog, null);
            TextField = view.FindViewById (Resource.Id.text_entry) as EditText;
            TextField.SetHint (HintResource);
            TextField.Text = Value;
            builder.SetView (view);
            builder.SetNegativeButton (Resource.String.text_dialog_cancel, CancelClicked);
            builder.SetPositiveButton (Resource.String.text_dialog_save, SaveClicked);
            return builder.Create ();
        }

        public override void OnResume ()
        {
            base.OnResume ();
            TextField.SelectAll ();
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
