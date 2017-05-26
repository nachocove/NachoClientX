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

using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class AddAttendeeDialog : NcDialogFragment
    {
        MessageComposeHeaderAddressField RequiredField;
        MessageComposeHeaderAddressField OptionalField;

        Action<string, string> SaveAction;

        public AddAttendeeDialog (Action<string, string> saveAction) : base ()
        {
            SaveAction = saveAction;
            RetainInstance = true;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            var view = Activity.LayoutInflater.Inflate (Resource.Layout.AddAttendeeDialog, null);
            RequiredField = view.FindViewById (Resource.Id.required) as MessageComposeHeaderAddressField;
            OptionalField = view.FindViewById (Resource.Id.optional) as MessageComposeHeaderAddressField;
            RequiredField.AddressField.AllowDuplicates (false);
            RequiredField.AddressField.Adapter = new ContactAddressAdapter (Context);
            OptionalField.AddressField.AllowDuplicates (false);
            OptionalField.AddressField.Adapter = new ContactAddressAdapter (Context);
            builder.SetView (view);
            builder.SetNegativeButton (Resource.String.add_attendee_cancel, CancelClicked);
            builder.SetPositiveButton (Resource.String.add_attendee_done, SaveClicked);
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
            RequiredField.AddressField.RequestFocus ();
            Dialog.Window.SetSoftInputMode (SoftInput.StateAlwaysVisible);
        }

        void SaveClicked (object sender, DialogClickEventArgs e)
        {
            var required = RequiredField.AddressField.AddressString;
            var optional = OptionalField.AddressField.AddressString;
            SaveAction (required, optional);
            Dismiss ();
        }

        void CancelClicked (object sender, DialogClickEventArgs e)
        {
            Dismiss ();
        }

        public override void OnDismiss (IDialogInterface dialog)
        {
            ((ContactAddressAdapter)RequiredField.AddressField.Adapter).Cleanup ();
            ((ContactAddressAdapter)OptionalField.AddressField.Adapter).Cleanup ();
            SaveAction = null;
            RequiredField = null;
            OptionalField = null;
            base.OnDismiss (dialog);
        }
    }
}
