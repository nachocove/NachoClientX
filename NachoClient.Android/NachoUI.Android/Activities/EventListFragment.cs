
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Graphics;

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
using Android.Widget;
using NachoPlatform;

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
        CalendarPagerView calendarPager;

        SwipeRefreshLayout mSwipeRefreshLayout;

        ImageView addButton;
        ImageView todayButton;

        private bool jumpToToday = false;
        bool isTouchScrolling;

        private class EventsObserver : Android.Database.DataSetObserver
        {

            Action Callback;

            public EventsObserver (Action callback)
            {
                Callback = callback;
            }

            public override void OnChanged ()
            {
                Callback ();
            }

        }

        EventsObserver observer;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            observer = new EventsObserver (DataSetChanged);
            eventListAdapter = new EventListAdapter (CreateEventOnDate);
            eventListAdapter.RegisterDataSetObserver (observer);
        }

        void DataSetChanged ()
        {
            if (calendarPager != null) {
                calendarPager.Update ();
            }
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.EventListFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            mSwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            mSwipeRefreshLayout.SetColorSchemeResources (Resource.Color.refresh_1, Resource.Color.refresh_2, Resource.Color.refresh_3);

            mSwipeRefreshLayout.Refresh += (object sender, EventArgs e) => {
                rearmRefreshTimer (3);
            };

            addButton = view.FindViewById<ImageView> (Resource.Id.right_button1);
            addButton.SetImageResource (Resource.Drawable.cal_add);
            addButton.Visibility = ViewStates.Visible;
            addButton.Click += AddButton_Click;

            todayButton = view.FindViewById<ImageView> (Resource.Id.right_button2);
            todayButton.Visibility = ViewStates.Visible;
            todayButton.Click += TodayButton_Click;
            DrawTodayButton ();

            calendarPager = view.FindViewById<CalendarPagerView> (Resource.Id.calendar_pager);
            calendarPager.DateSelected = PagerSelectedDate;
            calendarPager.HasEvents = PagerHasEvents;
            calendarPager.IsSupportedDate = PagerIsSupportedDate;

            // Highlight the tab bar icon of this activity
            var inboxImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.calendar_image);
            inboxImage.SetImageResource (Resource.Drawable.nav_calendar_active);

            listView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.Adapter = eventListAdapter;

            listView.ItemClick += ListView_ItemClick;

            listView.setMenuCreator ((menu) => {
                if (EVENT_CELL_TYPE == menu.getViewType ()) {
                    SwipeMenuItem lateItem = new SwipeMenuItem (Activity.ApplicationContext);
                    lateItem.setBackground (A.Drawable_NachoSwipeCalendarLate (this.Activity));
                    lateItem.setWidth (dp2px (90));
                    lateItem.setTitle ("I'm Late");
                    lateItem.setTitleSize (14);
                    lateItem.setTitleColor (A.Color_White);
                    lateItem.setIcon (A.Id_NachoSwipeCalendarLate);
                    lateItem.setId (LATE_TAG);
                    menu.addMenuItem (lateItem, SwipeMenu.SwipeSide.LEFT);

                    SwipeMenuItem forwardItem = new SwipeMenuItem (Activity.ApplicationContext);
                    forwardItem.setBackground (A.Drawable_NachoSwipeCalendarForward (this.Activity));
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
                var cal = CalendarHelper.GetMcCalendarRootForEvent (eventListAdapter [position].Id);
                switch (index) {
                case LATE_TAG:
                    if (null != cal) {
                        var outgoingMessage = McEmailMessage.MessageWithSubject (NcApplication.Instance.DefaultEmailAccount, "Re: " + cal.GetSubject ());
                        outgoingMessage.To = cal.OrganizerEmail;
                        StartActivity (MessageComposeActivity.InitialTextIntent (this.Activity, outgoingMessage, "Running late."));
                    }
                    break;
                case FORWARD_TAG:
                    if (null != cal) {
                        StartActivity (MessageComposeActivity.ForwardCalendarIntent (
                            this.Activity, cal.Id, McEmailMessage.MessageWithSubject (NcApplication.Instance.DefaultEmailAccount, "Fwd: " + cal.GetSubject ())));
                    }
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action index {0}", index));
                }
                return false;
            });

            listView.setOnSwipeStartListener ((position) => {
                mSwipeRefreshLayout.Enabled = false;
            });

            listView.setOnSwipeEndListener ((position) => {
                mSwipeRefreshLayout.Enabled = true;
            });

            listView.ScrollStateChanged += ListView_ScrollStateChanged;

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            if (jumpToToday) {
                jumpToToday = false;
                listView.SetSelection (eventListAdapter.PositionForToday);
            }

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            eventListAdapter.OnResume ();
        }

        public override void OnStop ()
        {
            base.OnStop ();
            eventListAdapter.OnStop ();
        }

        public override void OnDestroyView ()
        {
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            base.OnDestroyView ();
        }

        void DrawTodayButton ()
        {
            var date = DateTime.Now.ToLocalTime ().ToString(" d").Trim();
            var opts = new BitmapFactory.Options ();
            opts.InMutable = true;
            var image = BitmapFactory.DecodeResource (Resources, Resource.Drawable.calendar_empty_cal_alt, opts);
            var canvas = new Canvas (image);
            var paint = new Paint ();
            paint.TextSize = image.Height / 2.0f;
            paint.Color = Resources.GetColor(Resource.Color.NachoTeal);
            var bounds = new Rect ();
            paint.GetTextBounds (date, 0, date.Length, bounds);
            paint.TextAlign = Paint.Align.Center;
            canvas.DrawText (date, (int)(image.Width / 2.0f), (int)((image.Height + bounds.Height()) / 2.0f), paint);
            todayButton.SetImageBitmap (image);
        }

        void ListView_ScrollStateChanged (object sender, AbsListView.ScrollStateChangedEventArgs e)
        {
            if (e.ScrollState == ScrollState.TouchScroll) {
                isTouchScrolling = true;
                listView.Scroll += ListView_Scroll;
            } else if (isTouchScrolling && e.ScrollState == ScrollState.Idle) {
                listView.Scroll -= ListView_Scroll;
                isTouchScrolling = false;
            }
        }

        void ListView_Scroll (object sender, AbsListView.ScrollEventArgs e)
        {
            var position = listView.FirstVisiblePosition;
            var date = eventListAdapter.DateForPosition (position);
            calendarPager.SetHighlightedDate (date);
        }

        public void StartAtToday ()
        {
            jumpToToday = true;
        }

        void PagerSelectedDate (DateTime date)
        {
            var position = eventListAdapter.PositionForDate (date);
            ScrollToPosition (position);
        }

        const int MAX_SCROLL = 20;

        void ScrollToPosition (int position)
        {
            // The internet says that we might not always get a ScrollState.Idle event in ListView_ScrollStateChanged,
            // so protect against that case by making sure we're not listening for scroll before starting a smooth scroll.
            // Since we call this function when we've already set the pager to a specific date, we don't want our scroll
            // listener running and animating the pager away from and then back to the date it's already showing
            if (isTouchScrolling) {
                listView.Scroll -= ListView_Scroll;
                isTouchScrolling = false;
            }
            // The duration parameter for SmoothScrollToPositionFromTop is a lower bound, not a hard value.
            // If there is a long way to scroll, jump most of the way there and then scroll the rest of the way.
            listView.Post (() => {
                int existingPosition = listView.FirstVisiblePosition;
                if (existingPosition + MAX_SCROLL < position) {
                    listView.SetSelection (position - MAX_SCROLL);
                } else if (existingPosition - MAX_SCROLL > position) {
                    listView.SetSelection (position + MAX_SCROLL);
                }
                listView.SmoothScrollToPositionFromTop (position, offset: 0, duration: 200);
            });
        }

        bool PagerHasEvents (DateTime date)
        {
            return eventListAdapter.HasEvents (date);
        }

        bool PagerIsSupportedDate (DateTime date)
        {
            return eventListAdapter.IsSupportedDate (date);
        }

        void TodayButton_Click (object sender, EventArgs e)
        {
            calendarPager.SetHighlightedDate (DateTime.Today);
            ScrollToPosition (eventListAdapter.PositionForToday);
        }

        void ListView_ItemClick (object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            var ev = eventListAdapter [e.Position];
            StartActivity (EventViewActivity.ShowEventIntent (Activity, ev));
        }

        void AddButton_Click (object sender, EventArgs e)
        {
            StartActivity (EventEditActivity.NewEventIntent (this.Activity));
        }

        void CreateEventOnDate (DateTime date)
        {
            StartActivity (EventEditActivity.NewEventOnDayIntent (this.Activity, date));
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

        void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {

            case NcResult.SubKindEnum.Info_ExecutionContextChanged:
                if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                    DrawTodayButton ();
                    calendarPager.Update ();
                }
                break;
            }
        }
    }

    public class EventListAdapter : Android.Widget.BaseAdapter<McEvent>
    {
        public delegate void CreateEventOnDateDelegate (DateTime date);

        protected INcEventProvider eventCalendarMap;
        protected CreateEventOnDateDelegate createEventOnDateCallback;

        public EventListAdapter (CreateEventOnDateDelegate callback)
        {
            eventCalendarMap = NachoPlatform.Calendars.Instance.EventProviderInstance;
            createEventOnDateCallback = callback;
        }

        public void OnResume ()
        {
            eventCalendarMap.UiRefresh = NotifyDataSetChanged;
            NotifyDataSetChanged ();
        }

        public void OnStop ()
        {
            eventCalendarMap.UiRefresh = null;
        }

        public int PositionForToday {
            get {
                return PositionForDate (DateTime.Today);
            }
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
                View cellView;
                if (null == convertView) {
                    cellView = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.EventDateCell, parent, false);
                    cellView.FindViewById<ImageView> (Resource.Id.event_date_add).Click += AddButton_Click;
                } else {
                    cellView = convertView;
                }
                Bind.BindEventDateCell (eventCalendarMap.GetDateUsingDayIndex (day), cellView);
                return cellView;
            } else {
                var cellView = convertView ?? LayoutInflater.From (parent.Context).Inflate (Resource.Layout.EventCell, parent, false);
                Bind.BindEventCell (eventCalendarMap.GetEvent (day, item), cellView);
                return cellView;
            }
        }

        private void AddButton_Click (object sender, EventArgs e)
        {
            DateTime date = ((JavaObjectWrapper<DateTime>)(((ImageView)sender).GetTag (Resource.Id.event_date_add))).Item;
            createEventOnDateCallback (date);
        }

        public int PositionForDate (DateTime date)
        {
            var day = eventCalendarMap.IndexOfDate (date);
            if (0 <= day) {
                return eventCalendarMap.IndexFromDayItem (day, -1);
            }
            return -1;
        }

        public DateTime DateForPosition (int position)
        {
            int day;
            int item;
            eventCalendarMap.IndexToDayItem (position, out day, out item);
            return eventCalendarMap.GetDateUsingDayIndex (day);
        }

        public bool HasEvents (DateTime date)
        {
            return IsSupportedDate (date) && 0 < eventCalendarMap.NumberOfItemsForDay (eventCalendarMap.IndexOfDate (date));
        }

        public bool IsSupportedDate (DateTime date)
        {
            ExtendCalendarIfNecessary (date);
            return 0 <= eventCalendarMap.IndexOfDate (date);
        }

        private void ExtendCalendarIfNecessary (DateTime date)
        {
            // The calendar can only be extended into the future.  So ignore dates in the past.
            if (date > DateTime.Now && 0 > eventCalendarMap.IndexOfDate (date)) {
                // Extending the calendar is expensive, so extend it two months at a time,
                // not just up to the given date.
                DateTime local = date.ToLocalTime ();
                DateTime newLimit = new DateTime (local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Local).AddMonths (2);
                eventCalendarMap.ExtendEventMap (newLimit);
                NotifyDataSetChanged ();
            }
        }
    }
}

