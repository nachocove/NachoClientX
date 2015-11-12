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
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "MoreActivity")]            
    public class MoreActivity : NcTabBarActivity
    {

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.MoreActivity);

            this.RequestedOrientation = Android.Content.PM.ScreenOrientation.Nosensor;

            var moreFragment = MoreFragment.newInstance ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, moreFragment).Commit ();
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

        public override void SwitchAccount (McAccount account)
        {
            base.SwitchAccount (account);
        }
    }
}
