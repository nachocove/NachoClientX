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
    public class EventReminderPickerDialog : NcDialogFragment
    {

        class Option
        {
            public uint Value;
        }

        Option [] Options = new Option [] {
            new Option () { Value = 0 },
            new Option () { Value = 1 },
            new Option () { Value = 5 },
            new Option () { Value = 15 },
            new Option () { Value = 30 },
            new Option () { Value = 60 },
            new Option () { Value = 60 * 24 },
            new Option () { Value = 60 * 24 * 2 },
            new Option () { Value = 60 * 24 * 7 }
        };

        bool IsReminderSet;
        uint Reminder;
        Action<bool, uint> DismissAction;

        public EventReminderPickerDialog (bool isReminderSet, uint reminder) : base ()
        {
            IsReminderSet = isReminderSet;
            Reminder = reminder;
            RetainInstance = true;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            builder.SetTitle (GetString (Resource.String.event_reminder));
            var names = new List<string> ();
            var selectedIndex = IsReminderSet ? -1 : 0;
            names.Add (Pretty.ReminderString (NachoPlatform.Strings.Instance, false, 0));
            for (var i = 0; i < Options.Length; ++i) {
                names.Add (Pretty.ReminderString (NachoPlatform.Strings.Instance, true, Options [i].Value));
                if (IsReminderSet && Options [i].Value == Reminder) {
                    selectedIndex = i + 1;
                }
            }
            if (selectedIndex == -1) {
                selectedIndex = names.Count;
                names.Add (Pretty.ReminderString (NachoPlatform.Strings.Instance, true, Reminder));
            }
            builder.SetSingleChoiceItems (names.ToArray (), selectedIndex, OptionSelected);
            return builder.Create ();
        }

        void OptionSelected (object sender, DialogClickEventArgs e)
        {
            var index = e.Which;
            if (index == 0) {
                IsReminderSet = false;
            } else {
                index -= 1;
                if (index < Options.Length) {
                    IsReminderSet = true;
                    Reminder = Options [index].Value;
                }
            }
            Dismiss ();
        }


        public void Show (FragmentManager manager, string tag, Action<bool, uint> dismissAction)
        {
            DismissAction = dismissAction;
            Show (manager, tag);
        }

        public override void OnDismiss (IDialogInterface dialog)
        {
            var action = DismissAction;
            DismissAction = null;
            if (action != null) {
                action (IsReminderSet, Reminder);
            }
            base.OnDismiss (dialog);
        }
    }
}
