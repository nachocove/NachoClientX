
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
            RefreshEventsIfVisible ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        protected void RefreshEventsIfVisible ()
        {
            eventCalendarMap = new NcAllEventsCalendarMap ();
            eventCalendarMap.Refresh (completionAction: null);
        }

        public override long GetItemId (int position)
        {
            var ev = eventCalendarMap.GetEventByIndex (position);
            return ev.Id;
        }

        public override int Count {
            get {
                return eventCalendarMap.NumberOfEvents ();
            }
        }

        public override McEvent this [int position] {  
            get {
                return eventCalendarMap.GetEventByIndex (position);
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.EventCell, parent, false);
            }
            var ev = this [position];
            Bind.BindEventCell (ev, view);

            return view;
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

