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
using Android.Support.V7.Widget;
using Android.Views.InputMethods;

using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class EventEditFragment : Fragment, EventEditAdapter.Listener
	{

        public McCalendar CalendarItem;
        EventEditAdapter Adapter;

        #region Subviews

        RecyclerView ListView;

		void FindSubviews (View view)
		{
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (view.Context));
		}

		void ClearSubviews ()
		{
            ListView = null;
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
            Adapter = new EventEditAdapter (this, CalendarItem);
            ListView.SetAdapter (Adapter);
			return view;
		}

		public override void OnDestroyView ()
		{
			ClearSubviews ();
			base.OnDestroyView ();
		}

        #endregion

        #region Listener

        #endregion

        public void EndEditing ()
        {
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (View.WindowToken, HideSoftInputFlags.NotAlways);
        }

	}

    public class EventEditAdapter : GroupedListRecyclerViewAdapter
    {
        public interface Listener
        {
        }

        WeakReference<Listener> WeakListener;

        McCalendar CalendarItem;

        public EventEditAdapter (Listener listener, McCalendar calendarItem) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            CalendarItem = calendarItem;
        }

        public override int GroupCount {
            get {
                return 0;
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            return 0;
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            throw new NotImplementedException ();
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            throw new NotImplementedException ();
        }
    }
}
