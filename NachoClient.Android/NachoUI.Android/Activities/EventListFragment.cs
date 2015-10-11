﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;

//using Android.Util;
using Android.Views;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using Android.Graphics.Drawables;
using NachoCore.Brain;

namespace NachoClient.AndroidClient
{
    public class EventListFragment : Fragment
    {
        public const int DATE_HEADER_CELL_TYPE = 0;
        public const int EVENT_CELL_TYPE = 1;
        public const int NUM_CELL_TYPES = 2;

        private const int LATE_TAG = 1;
        private const int FORWARD_TAG = 2;

        SwipeMenuListView listView;
        EventListAdapter eventListAdapter;

        SwipeRefreshLayout mSwipeRefreshLayout;

        Android.Widget.ImageView addButton;

        public event EventHandler<McEvent> onEventClick;

        public static EventListFragment newInstance ()
        {
            var fragment = new EventListFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.EventListFragment, container, false);

            var activity = (NcActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            mSwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            mSwipeRefreshLayout.SetColorSchemeResources (Resource.Color.refresh_1, Resource.Color.refresh_2, Resource.Color.refresh_3);

            mSwipeRefreshLayout.Refresh += (object sender, EventArgs e) => {
                rearmRefreshTimer (3);
            };

            addButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            addButton.SetImageResource (Android.Resource.Drawable.IcMenuAdd);
            addButton.Visibility = Android.Views.ViewStates.Visible;
            addButton.Click += AddButton_Click;

            // Highlight the tab bar icon of this activity
            var inboxImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.calendar_image);
            inboxImage.SetImageResource (Resource.Drawable.nav_calendar_active);

            eventListAdapter = new EventListAdapter ();

            listView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.Adapter = eventListAdapter;

            listView.ItemClick += ListView_ItemClick;

            listView.setMenuCreator ((menu) => {
                if (EVENT_CELL_TYPE == menu.getViewType ()) {
                    SwipeMenuItem lateItem = new SwipeMenuItem (Activity.ApplicationContext);
                    lateItem.setBackground (new ColorDrawable (A.Color_NachoSwipeCalendarLate));
                    lateItem.setWidth (dp2px (90));
                    lateItem.setTitle ("I'm Late");
                    lateItem.setTitleSize (14);
                    lateItem.setTitleColor (A.Color_White);
                    lateItem.setIcon (A.Id_NachoSwipeCalendarLate);
                    lateItem.setId (LATE_TAG);
                    menu.addMenuItem (lateItem, SwipeMenu.SwipeSide.LEFT);

                    SwipeMenuItem forwardItem = new SwipeMenuItem (Activity.ApplicationContext);
                    forwardItem.setBackground (new ColorDrawable (A.Color_NachoSwipeCalendarForward));
                    forwardItem.setWidth (dp2px (90));
                    forwardItem.setTitle ("Forward");
                    forwardItem.setTitleSize (14);
                    forwardItem.setTitleColor (A.Color_White);
                    forwardItem.setIcon (A.Id_NachoSwipeCalendarForward);
                    forwardItem.setId (FORWARD_TAG);
                    menu.addMenuItem (forwardItem, SwipeMenu.SwipeSide.RIGHT);
                }
            });

            listView.setOnMenuItemClickListener (( position, menu, index) => {
                switch (index) {
                case LATE_TAG:
                    break;
                case FORWARD_TAG:
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action index {0}", index));
                }
                return false;
            });

            return view;
        }

        void ListView_ItemClick (object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            if (null != onEventClick) {
                onEventClick (this, eventListAdapter [e.Position]);
            }
        }

        void AddButton_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
//            intent.SetClass (this.Activity, typeof(EventEditActivity));
            StartActivity (intent);
        }

        protected void EndRefreshingOnUIThread (object sender)
        {
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                if (mSwipeRefreshLayout.Refreshing) {
                    mSwipeRefreshLayout.Refreshing = false;
                }
            });
        }

        NcTimer refreshTimer;

        void rearmRefreshTimer (int seconds)
        {
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
            refreshTimer = new NcTimer ("EventListFragment refresh", EndRefreshingOnUIThread, null, seconds * 1000, 0); 
        }

        void cancelRefreshTimer ()
        {
            if (mSwipeRefreshLayout.Refreshing) {
                EndRefreshingOnUIThread (null);
            }
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }

      
    }

    public class EventListAdapter : Android.Widget.BaseAdapter<McEvent>
    {
        protected INcEventProvider eventCalendarMap;

        public EventListAdapter ()
        {
            eventCalendarMap = new NcAllEventsCalendarMap ();
            eventCalendarMap.Refresh (() => {
                NotifyDataSetChanged ();
            });
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        protected void RefreshEventsIfVisible ()
        {
            eventCalendarMap.Refresh (() => {
                NotifyDataSetChanged ();
            });
        }

        public override long GetItemId (int position)
        {
            int day, item;
            eventCalendarMap.IndexToDayItem (position, out day, out item);
            if (-1 == item) {
                return eventCalendarMap.GetDateUsingDayIndex (day).Ticks;
            }
            return eventCalendarMap.GetEvent (day, item).Id;
        }

        public override int Count {
            get {
                return eventCalendarMap.NumberOfDays () + eventCalendarMap.NumberOfEvents ();
            }
        }

        public override McEvent this [int position] {  
            get {
                int day, item;
                eventCalendarMap.IndexToDayItem (position, out day, out item);
                if (-1 == item) {
                    return new McEvent ();
                }
                return eventCalendarMap.GetEvent (day, item);
            }
        }

        public override bool IsEnabled (int position)
        {
            int day, item;
            eventCalendarMap.IndexToDayItem (position, out day, out item);
            return -1 != item;
        }

        public override int ViewTypeCount {
            get {
                return EventListFragment.NUM_CELL_TYPES;
            }
        }

        public override int GetItemViewType (int position)
        {
            int day, item;
            eventCalendarMap.IndexToDayItem (position, out day, out item);
            if (-1 == item) {
                return EventListFragment.DATE_HEADER_CELL_TYPE;
            }
            return EventListFragment.EVENT_CELL_TYPE;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            int day, item;
            eventCalendarMap.IndexToDayItem (position, out day, out item);
            if (-1 == item) {
                var cellView = convertView ?? LayoutInflater.From (parent.Context).Inflate (Resource.Layout.EventDateCell, parent, false);
                Bind.BindEventDateCell (eventCalendarMap.GetDateUsingDayIndex (day), cellView);
                return cellView;
            } else {
                var cellView = convertView ?? LayoutInflater.From (parent.Context).Inflate (Resource.Layout.EventCell, parent, false);
                Bind.BindEventCell (eventCalendarMap.GetEvent (day, item), cellView);
                return cellView;
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_CalendarSetChanged:
                RefreshEventsIfVisible ();
                break;
            }
        }

    }
}

