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

namespace NachoClient.AndroidClient
{
    public class NcDialogFragment : DialogFragment
    {

        Action DismissAction;

        public void Show (FragmentManager manager, string tag, Action dismissAction)
        {
            DismissAction = dismissAction;
            Show (manager, tag);
        }

        public override void OnDismiss (IDialogInterface dialog)
        {
            var action = DismissAction;
            DismissAction = null;
            if (action != null) {
                action ();
            }
            base.OnDismiss (dialog);
        }

    }
}
