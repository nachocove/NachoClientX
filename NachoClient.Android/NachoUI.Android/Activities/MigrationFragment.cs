
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
    public class MigrationFragment : Fragment
    {
        int currentMigration = 0;
        int NumberOfMigrations = 2;

        public static MigrationFragment newInstance ()
        {
            var fragment = new MigrationFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.MigrationFragment, container, false);

            var progressBar = view.FindViewById<ProgressBar> (Resource.Id.progress);
            progressBar.Max = NumberOfMigrations;

            UpdateProgressMessage (view);

            return view;
        }

        void UpdateProgressMessage (View parentView)
        {
            var progressBar = parentView.FindViewById<ProgressBar> (Resource.Id.progress);
            if (0 < currentMigration) {
                progressBar.IncrementProgressBy (1);
            }

            var tv = parentView.FindViewById<TextView> (Resource.Id.message);

            if (currentMigration < NumberOfMigrations) {
                tv.Text = String.Format ("Updating your app with latest features... ({0} of {1})", currentMigration + 1, NumberOfMigrations);
            } else {
                tv.Text = String.Format ("Your app is now up to date.");
            }
        }
    }
}

