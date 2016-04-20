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
        private const string MORE_FRAGMENT_TAG = "MoreFragment";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.MoreActivity);

            if (null == bundle || null == FragmentManager.FindFragmentByTag<MoreFragment> (MORE_FRAGMENT_TAG)) {
                var moreFragment = MoreFragment.newInstance ();
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, moreFragment, MORE_FRAGMENT_TAG).Commit ();
            }
        }
    }
}
