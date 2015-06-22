
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

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.MigrationFragment, container, false);
            var tv = view.FindViewById<TextView> (Resource.Id.textview);
            tv.Text = "Migration fragment";

            var progressBar = view.FindViewById<ProgressBar> (Resource.Id.progress);
            progressBar.Max = NumberOfMigrations;

            UpdateProgressMessage (view);

            var demoButton = view.FindViewById<Button> (Resource.Id.btnDemo);
            demoButton.Click += DemoButton_Click;

            return view;
        }

        void DemoButton_Click (object sender, EventArgs e)
        {
            currentMigration += 1;
            if (currentMigration > 2) {
                var parent = (MainActivity)Activity;
                parent.MigrationFinished ();
            } else {
                UpdateProgressMessage (View);
            }
        }

        void UpdateProgressMessage(View parentView)
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

