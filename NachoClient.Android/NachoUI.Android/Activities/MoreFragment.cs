﻿
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
    public class MoreFragment : Fragment
    {

        public static MoreFragment newInstance ()
        {
            var fragment = new MoreFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.MoreFragment, container, false);

            var activity = (NcActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            var folderView = view.FindViewById<View> (Resource.Id.mail);
            folderView.Click += FolderView_Click;

            var supportView = view.FindViewById<View> (Resource.Id.support);
            supportView.Click += SupportView_Click;

            var aboutView = view.FindViewById<View> (Resource.Id.about);
            aboutView.Click += AboutView_Click;

            return view;
        }

        void AboutView_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(AboutActivity));
            StartActivity (intent);    
        }

        void FolderView_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(FoldersActivity));
            StartActivity (intent);
        }

        void SupportView_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(SupportActivity));
            StartActivity (intent);
        }
    }
}

