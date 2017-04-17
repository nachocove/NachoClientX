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

namespace NachoClient.AndroidClient
{
    public class DaysToSyncPickerDialog : NcDialogFragment
    {

        class Option
        {
            public NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode Value;
        }

        Option [] Options = new Option [] {
            new Option () { Value = NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.SyncAll_0 },
            new Option () { Value = NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.OneMonth_5 }
        };

        private McAccount Account;

        public DaysToSyncPickerDialog (McAccount account) : base ()
        {
            Account = account;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            builder.SetTitle (GetString (Resource.String.account_sync));
            var names = new string [Options.Length];
            var selectedIndex = 0;
            for (var i = 0; i < Options.Length; ++i) {
                names [i] = Pretty.MaxAgeFilter (Options [i].Value);
                if (Options [i].Value == Account.DaysToSyncEmail) {
                    selectedIndex = i;
                }
            }
            builder.SetSingleChoiceItems (names, selectedIndex, OptionSelected);
            return builder.Create ();
        }

        void OptionSelected (object sender, DialogClickEventArgs e)
        {
            var option = Options [e.Which];
            Account.DaysToSyncEmail = option.Value;
            Account.Update ();
            Dismiss ();
        }

    }
}
