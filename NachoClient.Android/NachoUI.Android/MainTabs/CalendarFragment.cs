//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Support.V4.App;
using Android.Support.Design.Widget;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace NachoClient.AndroidClient
{
	public class CalendarFragment : Fragment, MainTabsActivity.Tab
	{

		#region Tab Interface

        public bool OnCreateOptionsMenu (MainTabsActivity tabActivity, IMenu menu)
        {
            return false;
        }

		public void OnTabSelected (MainTabsActivity tabActivity)
		{
            tabActivity.HideActionButton ();
		}

		public void OnTabUnselected (MainTabsActivity tabActivity)
		{
		}

        public void OnAccountSwitched (MainTabsActivity tabActivity)
        {
        }

        public bool OnOptionsItemSelected (MainTabsActivity tabActivity, IMenuItem item)
        {
            return false;
        }

		#endregion

		#region Fragment Lifecycle

		public override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			// Create your fragment here
		}

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			// Use this to return your custom view for this Fragment
			// return inflater.Inflate(Resource.Layout.YourFragment, container, false);

			return base.OnCreateView (inflater, container, savedInstanceState);
		}

		#endregion

	}
}
