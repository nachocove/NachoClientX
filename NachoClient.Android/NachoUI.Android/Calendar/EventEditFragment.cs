//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
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
	public class EventEditFragment : Fragment
	{

		#region Subviews

		void FindSubviews (View view)
		{
		}

		void ClearSubviews ()
		{
		}

		#endregion

		#region Fragment Lifecycle

		public override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
		}

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			var view = inflater.Inflate (Resource.Layout.EventEditFragment, container, false);
			FindSubviews (view);
			return view;
		}

		public override void OnDestroyView ()
		{
			ClearSubviews ();
			base.OnDestroyView ();
		}

		#endregion

	}
}
