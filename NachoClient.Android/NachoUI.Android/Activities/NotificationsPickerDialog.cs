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
    public class NotificationsPickerDialog : NcDialogFragment
    {

        class Option
        {
            public McAccount.NotificationConfigurationEnum Value;
        }

        Option [] Options = new Option [] {
            new Option () { Value = McAccount.NotificationConfigurationEnum.ALLOW_HOT_2 },
            new Option () { Value = McAccount.NotificationConfigurationEnum.ALLOW_VIP_4 },
            new Option () { Value = McAccount.NotificationConfigurationEnum.ALLOW_INBOX_64 }
        };

        private McAccount Account;

        public NotificationsPickerDialog (McAccount account) : base ()
        {
            Account = account;
            RetainInstance = true;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            builder.SetTitle (GetString (Resource.String.account_notifications));
            var names = new string [Options.Length];
            var selected = new bool [Options.Length];
            for (var i = 0; i < Options.Length; ++i) {
                names [i] = Pretty.NotificationConfiguration (Options [i].Value);
                selected[i] = (Account.NotificationConfiguration & Options [i].Value) != 0;
            }
            builder.SetMultiChoiceItems (names, selected, OptionSelected);
            builder.SetNeutralButton (Resource.String.account_notifications_done, (sender, e) => { Dismiss (); });
            return builder.Create ();
        }

        void OptionSelected (object sender, DialogMultiChoiceClickEventArgs e)
        {
            var option = Options [e.Which];
            if (e.IsChecked) {
                Account.NotificationConfiguration |= option.Value;
            } else {
                Account.NotificationConfiguration &= ~option.Value;
            }
            Account.Update ();
        }

    }
}
