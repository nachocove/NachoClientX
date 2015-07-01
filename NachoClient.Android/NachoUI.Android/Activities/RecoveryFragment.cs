
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
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.RecoveryFragment, container, false);
            var tv = view.FindViewById<TextView> (Resource.Id.textview);
            tv.Text = "Recovery fragment";

            var rv = view.FindViewById<TextView> (Resource.Id.message);
            rv.Text = "We are sorry a crash occurred. We are recovering.";

            var demoButton = view.FindViewById<Button> (Resource.Id.btnDemo);
            demoButton.Click += DemoButton_Click;

            return view;
        }

        void DemoButton_Click (object sender, EventArgs e)
        {
            var parent = (MainActivity)Activity;
            parent.RecoveryFinished ();
        }
    }
}

