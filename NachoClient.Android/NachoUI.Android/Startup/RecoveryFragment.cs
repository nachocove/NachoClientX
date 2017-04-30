
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
    public class RecoveryFragment : Fragment
    {
        public static RecoveryFragment newInstance ()
        {
            var fragment = new RecoveryFragment ();
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.RecoveryFragment, container, false);

            var rv = view.FindViewById<TextView> (Resource.Id.message);
            rv.Text = "We are sorry a crash occurred. We are recovering.";

            return view;
        }

    }
}

