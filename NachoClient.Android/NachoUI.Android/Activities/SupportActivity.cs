﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "SupportActivity", WindowSoftInputMode = Android.Views.SoftInput.AdjustResize)]            
    public class SupportActivity : NcActivity
    {

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.SupportActivity);

            var supportFragment = SupportFragment.newInstance ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, supportFragment).Commit ();
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is SupportMessageFragment) {
                this.FragmentManager.PopBackStack ();
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

        public override void SwitchAccount (McAccount account)
        {
            base.SwitchAccount (account);
        }

        public void EmailSupportClick ()
        {
            var supportMessageFragment = SupportMessageFragment.newInstance ();
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, supportMessageFragment).AddToBackStack ("Message").Commit ();
        }

        public void MessageSentCallback()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is SupportMessageFragment) {
                this.FragmentManager.PopBackStack ();
            }
        }

    }
}