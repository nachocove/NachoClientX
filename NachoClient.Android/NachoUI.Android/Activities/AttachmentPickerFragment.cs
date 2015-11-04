//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
    public interface AttachmentPickerFragmentDelegate {
    }

    public class AttachmentPickerFragment : DialogFragment
    {

        public AttachmentPickerFragmentDelegate Delegate;

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            var inflater = Activity.LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.AttachmentPickerFragment, null);
            builder.SetTitle ("Attach");
            builder.SetView (view);
            return builder.Create ();
        }

        public override void OnAttach (Activity activity)
        {
            base.OnAttach (activity);
        }
    }
}

