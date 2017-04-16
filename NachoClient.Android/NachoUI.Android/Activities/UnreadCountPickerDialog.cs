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
    public class UnreadCountPickerDialog : NcDialogFragment
    {

        class Option
        {
            public int NameResource;
            public EmailHelper.ShowUnreadEnum Value;
        }

        Option [] Options = new Option [] {
            new Option () { NameResource = Resource.String.settings_unread_count_all, Value = EmailHelper.ShowUnreadEnum.AllMessages },
            new Option () { NameResource = Resource.String.settings_unread_count_today, Value = EmailHelper.ShowUnreadEnum.TodaysMessages },
            new Option () { NameResource = Resource.String.settings_unread_count_recent, Value = EmailHelper.ShowUnreadEnum.RecentMessages }
        };

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            builder.SetTitle (GetString (Resource.String.settings_unread_count));
            var names = new string [Options.Length];
            var selectedValue = EmailHelper.HowToDisplayUnreadCount ();
            var selectedIndex = 0;
            for (var i = 0; i < Options.Length; ++i) {
                names [i] = GetString (Options [i].NameResource);
                if (Options [i].Value == selectedValue) {
                    selectedIndex = i;
                }
            }
            builder.SetSingleChoiceItems (names, selectedIndex, OptionSelected);
            return builder.Create ();
        }

        void OptionSelected (object sender, DialogClickEventArgs e)
        {
            var option = Options [e.Which];
            EmailHelper.SetHowToDisplayUnreadCount (option.Value);
            Dismiss ();
        }

    }
}
