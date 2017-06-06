﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
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
using Android.Support.V7.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public class CalendarFragment : Fragment, MainTabsActivity.Tab, CalendarAdapter.Listener
    {

        private const int REQUEST_SHOW_EVENT = 1;

        INcEventProvider Events;
        CalendarAdapter Adapter;

        #region Tab Interface

        public bool OnCreateOptionsMenu (MainTabsActivity tabActivity, IMenu menu)
        {
            tabActivity.MenuInflater.Inflate (Resource.Menu.calendar, menu);
            // TODO: Customize today button with today's date
            return true;
        }

        public void OnTabSelected (MainTabsActivity tabActivity)
        {
            var account = NcApplication.Instance.DefaultCalendarAccount;
            if (account == null || account.AccountType == McAccount.AccountTypeEnum.Device) {
                tabActivity.HideActionButton ();
            } else {
                tabActivity.ShowActionButton (Resource.Drawable.floating_action_new_event, ActionButtonClicked);
            }
        }

        public void OnTabUnselected (MainTabsActivity tabActivity)
        {
        }

        public void OnAccountSwitched (MainTabsActivity tabActivity)
        {
            // Calendar always shows all accounts, so there's nothing we need to do on account switch
        }

        public bool OnOptionsItemSelected (MainTabsActivity tabActivity, IMenuItem item)
        {
            switch (item.ItemId) {
            case Resource.Id.today:
                GoToToday (animated: true);
                return true;
            }
            return false;
        }

        #endregion

        #region Subviews

        CalendarPagerView PagerView;
        RecyclerView ListView;

        void FindSubviews (View view)
        {
            PagerView = view.FindViewById (Resource.Id.pager) as CalendarPagerView;
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (view.Context));
            ListView.ScrollChange += ListViewScrolled;
            PagerView.DateSelected = PagerSelectedDate;
            PagerView.HasEvents = PagerHasEvents;
            PagerView.IsSupportedDate = PagerIsSupportedDate;
        }

        void ClearSubviews ()
        {
            PagerView.DateSelected = null;
            PagerView.HasEvents = null;
            PagerView.IsSupportedDate = null;
            PagerView = null;
            ListView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            Events = NachoPlatform.Calendars.Instance.EventProviderInstance;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.CalendarFragment, container, false);
            FindSubviews (view);
            Adapter = new CalendarAdapter (this, Events);
            ListView.SetAdapter (Adapter);
            GoToToday (animated: false);
            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            Events.UiRefresh = () => {
                Adapter.NotifyDataSetChanged ();
            };
        }

        public override void OnPause ()
        {
            Events.UiRefresh = null;
            base.OnPause ();
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        public override void OnActivityResult (int requestCode, int resultCode, Intent data)
        {
            switch (requestCode) {
            case REQUEST_SHOW_EVENT:
                HandleShowEventResult ((Android.App.Result)resultCode, data);
                break;
            default:
                base.OnActivityResult (requestCode, resultCode, data);
                break;
            }
        }

        #endregion

        #region Pager

        void PagerSelectedDate (DateTime date)
        {
            ScrollToDate (date, animated: true);
        }

        bool PagerHasEvents (DateTime date)
        {
            return Adapter.HasEvents (date);
        }

        bool PagerIsSupportedDate (DateTime date)
        {
            return Adapter.IsSupportedDate (date);
        }

        void ListViewScrolled (object sender, View.ScrollChangeEventArgs e)
        {
            var layoutManager = ListView.GetLayoutManager () as LinearLayoutManager;
            var position = layoutManager.FindFirstVisibleItemPosition ();
            var date = Adapter.DateForPosition (position);
            PagerView.SetHighlightedDate (date);
        }

        #endregion

        #region User Actions

        void ActionButtonClicked (object sender, EventArgs args)
        {
            ShowNewEvent ();
        }

        #endregion

        #region Context Menus

        bool IsContextMenuOpen = false;

        public void OnContextMenuClosed (IMenu menu)
        {
        	IsContextMenuOpen = false;
        }

        public override bool OnContextItemSelected (IMenuItem item)
        {
            var groupPosition = -1;
            var position = -1;
            var eventId = -1;
            if (item.Intent != null && item.Intent.HasExtra (CalendarAdapter.EXTRA_GROUP_POSITION)) {
                groupPosition = item.Intent.Extras.GetInt (CalendarAdapter.EXTRA_GROUP_POSITION);
            }
            if (item.Intent != null && item.Intent.HasExtra (CalendarAdapter.EXTRA_POSITION)) {
                position = item.Intent.Extras.GetInt (CalendarAdapter.EXTRA_POSITION);
            }
            if (item.Intent != null && item.Intent.HasExtra (CalendarAdapter.EXTRA_EVENT_ID)) {
                eventId = item.Intent.Extras.GetInt (CalendarAdapter.EXTRA_EVENT_ID);
            }
            if (groupPosition >= 0 && position >= 0 && eventId >= 0) {
                var calendarEvent = Events.GetEvent (groupPosition, position);
                if (calendarEvent.Id != eventId) {
                    calendarEvent = McEvent.QueryById<McEvent> (eventId);
                }
                switch (item.ItemId) {
                case Resource.Id.forward:
                    ShowForward (calendarEvent);
                    return true;
                case Resource.Id.late:
                    ShowRunningLate (calendarEvent);
                    return true;
                }
            }
            return base.OnContextItemSelected (item);
        }

        #endregion

        #region Listener

        public void OnEventSelected (McEvent calendarEvent)
        {
            ShowEvent (calendarEvent);
        }

        public void OnEventCreateRequested (DateTime day)
        {
            if (day.Date.Equals (DateTime.Today)) {
                ShowNewEvent ();
            } else {
                var morning = day.Date.AddHours (9.0f);
                ShowNewEvent (morning);
            }
        }

        public void OnContextMenuCreated ()
        {
            IsContextMenuOpen = true;
        }

        #endregion

        #region Private Helpers

        void GoToToday (bool animated)
        {
            PagerView.SetHighlightedDate (DateTime.Today);
            ScrollToDate (DateTime.Today, animated: animated);
        }

        void ShowNewEvent ()
        {
            var account = NcApplication.Instance.DefaultCalendarAccount;
            var intent = EventEditActivity.BuildNewEventIntent (Activity, account.Id);
			StartActivity (intent);
        }

        void ShowNewEvent (DateTime date)
        {
            var account = NcApplication.Instance.DefaultCalendarAccount;
            var intent = EventEditActivity.BuildNewEventIntent (Activity, account.Id, start: date);
            StartActivity (intent);
        }

        void ShowEvent (McEvent calendarEvent)
        {
            var intent = EventViewActivity.BuildIntent (Activity, calendarEvent);
            StartActivityForResult (intent, REQUEST_SHOW_EVENT);
        }

        void HandleShowEventResult (Android.App.Result result, Intent data)
        {
            if (result == Android.App.Result.Ok) {
                if (data != null) {
                    if (data.Action == EventViewActivity.ACTION_DELETE) {
                        // TODO: remove event from list view
                    }
                }
            }
        }

        int PositionForDate (DateTime date)
        {
            var index = Events.IndexOfDate (date);
            if (index >= 0) {
                return Events.IndexFromDayItem (index, -1);
            }
            return -1;
        }

        DateTime DateForPosition (int position)
        {
        	int day;
        	int item;
            Events.IndexToDayItem (position, out day, out item);
            return Events.GetDateUsingDayIndex (day);
        }

        void ScrollToDate (DateTime date, bool animated)
        {
            var position = PositionForDate (date);
            ScrollToPosition (position, animated: animated);
        }

        void ScrollToPosition (int position, bool animated)
        {
            var layoutManager = ListView.GetLayoutManager () as LinearLayoutManager;
            layoutManager.ScrollToPositionWithOffset (position, 0);
            //if (!animated) {
            //    ListView.ScrollToPosition (position);
            //} else {
            //    ListView.SmoothScrollToPosition (position);
            //}
        }

        void ShowRunningLate (McEvent calendarEvent)
        {
            var calendarItem = calendarEvent.Calendar;
            if (calendarItem != null) {
                var account = McAccount.EmailAccountForCalendar (calendarItem);
                var subject = EmailHelper.CreateInitialSubjectLine (EmailHelper.Action.Reply, calendarItem.Subject);
                var message = McEmailMessage.MessageWithSubject (account, calendarItem.Subject);
                message.To = calendarItem.OrganizerEmail;
                var intent = MessageComposeActivity.ForwardCalendarIntent (Activity, calendarItem.Id, message);
                StartActivity (intent);
            }
        }

        void ShowForward (McEvent calendarEvent)
        {
            var calendarItem = calendarEvent.Calendar;
            if (calendarItem != null) {
                var account = McAccount.EmailAccountForCalendar (calendarItem);
                var subject = EmailHelper.CreateInitialSubjectLine (EmailHelper.Action.Forward, calendarItem.Subject);
                var message = McEmailMessage.MessageWithSubject (account, subject);
                var intent = MessageComposeActivity.ForwardCalendarIntent (Activity, calendarItem.Id, message);
				StartActivity (intent);
            }
        }

        #endregion
    }

    public class CalendarAdapter : GroupedListRecyclerViewAdapter
    {

        public const string EXTRA_GROUP_POSITION = "NachoClient.AndroidClient.MessageListAdapter.EXTRA_GROUP_POSITION";
        public const string EXTRA_POSITION = "NachoClient.AndroidClient.MessageListAdapter.EXTRA_POSITION";
        public const string EXTRA_EVENT_ID = "NachoClient.AndroidClient.MessageListAdapter.EXTRA_EVENT_ID";

        public interface Listener
        {
            void OnEventSelected (McEvent calendarEvent);
            void OnEventCreateRequested (DateTime day);
            void OnContextMenuCreated ();
        }

        enum ViewType
        {
            DayHeader,
            Event
        }

        INcEventProvider Events;
        WeakReference<Listener> WeakListener;

        public CalendarAdapter (Listener listener, INcEventProvider events) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            Events = events;
        }

        public override bool HasFooters {
            get {
                return false;
            }
        }

        public bool HasEvents (DateTime date)
        {
            return IsSupportedDate (date) && Events.NumberOfItemsForDay (Events.IndexOfDate (date)) > 0;
        }

        public bool IsSupportedDate (DateTime date)
        {
        	//ExtendCalendarIfNecessary (date);
            return Events.IndexOfDate (date) >= 0;
        }

        public DateTime DateForPosition (int position)
        {
        	int day;
        	int item;
        	Events.IndexToDayItem (position, out day, out item);
        	return Events.GetDateUsingDayIndex (day);
        }

        public override int GroupCount {
            get {
                return Events.NumberOfDays ();
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            return Events.NumberOfItemsForDay (groupPosition);
        }

        public override int GetHeaderViewType (int groupPosition)
        {
            return (int)ViewType.DayHeader;
        }

        public override int GetItemViewType (int groupPosition, int position)
        {
            return (int)ViewType.Event;
        }

        public override void OnBindHeaderViewHolder (RecyclerView.ViewHolder holder, int groupPosition)
        {
            var day = Events.GetDateUsingDayIndex (groupPosition);
            var headerHolder = (holder as DayHeaderViewHolder);
            headerHolder.SetDay (day);
            headerHolder.SetAddHandler ((sender, e) => {
                Listener listener;
                if (WeakListener.TryGetTarget (out listener)) {
                    listener.OnEventCreateRequested (day);
                }
            });
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            switch ((ViewType)viewType) {
            case ViewType.DayHeader:
                return DayHeaderViewHolder.Create (parent);
            case ViewType.Event:
                var eventHolder = EventViewHolder.Create (parent);
                eventHolder.ItemView.ContextMenuCreated += (sender, e) => {
                    ItemContextMenuCreated (eventHolder.groupPosition, eventHolder.itemPosition, e.Menu);
                };
                return eventHolder;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("CalendarFragment.OnCreateGroupedViewHolder unexpected viewType: {0}", viewType));
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            var calendarEvent = Events.GetEvent (groupPosition, position);
            var eventHolder = (holder as EventViewHolder);
            eventHolder.SetEvent (calendarEvent);
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            var calendarEvent = Events.GetEvent (groupPosition, position);
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                listener.OnEventSelected (calendarEvent);
            }
        }

        void ItemContextMenuCreated (int groupPosition, int position, IContextMenu menu)
        {
            var calendarEvent = Events.GetEvent (groupPosition, position);
            var intent = new Intent ();
            intent.PutExtra (EXTRA_GROUP_POSITION, groupPosition);
            intent.PutExtra (EXTRA_POSITION, position);
            intent.PutExtra (EXTRA_EVENT_ID, calendarEvent.Id);
            int order = 0;
            List<IMenuItem> items = new List<IMenuItem> ();
            if (calendarEvent.HasNonSelfOrganizer) {
                items.Add (menu.Add (0, Resource.Id.late, order++, Resource.String.calendar_event_item_action_late));
            }
            items.Add (menu.Add (0, Resource.Id.forward, order++, Resource.String.calendar_event_item_action_forward));
            foreach (var item in items) {
                item.SetIntent (intent);
            }
            menu.SetHeaderTitle (calendarEvent.Subject);
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                listener.OnContextMenuCreated ();
            }
        }

        class EventViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            View DotView;
            View LocationGroup;
            Android.Widget.TextView TitleLabel;
            Android.Widget.TextView DurationLabel;
            Android.Widget.TextView LocationLabel;

            public static EventViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.CalendarListEventItem, parent, false);
                return new EventViewHolder (view);
            }
                                                  
            public EventViewHolder (View view) : base (view)
            {
                DotView = view.FindViewById (Resource.Id.dot);
                LocationGroup = view.FindViewById (Resource.Id.event_location_group);
                TitleLabel = view.FindViewById (Resource.Id.event_title) as Android.Widget.TextView;
                DurationLabel = view.FindViewById (Resource.Id.event_duration) as Android.Widget.TextView;
                LocationLabel = view.FindViewById (Resource.Id.event_location) as Android.Widget.TextView;
            }

            public void SetEvent (McEvent calendarEvent)
            {
                TitleLabel.Text = calendarEvent.Subject ?? "";
                LocationLabel.Text = calendarEvent.Location ?? "";
                if (String.IsNullOrEmpty (calendarEvent.Location)) {
                    LocationGroup.Visibility = ViewStates.Gone;
                } else {
                    LocationGroup.Visibility = ViewStates.Visible;
                }
                if (calendarEvent.AllDayEvent) {
                    DurationLabel.SetText (Resource.String.calendar_event_item_all_day);
                } else {
                    var duration = Pretty.Time (calendarEvent.StartTime);
                    if (calendarEvent.EndTime > calendarEvent.StartTime) {
                        duration += ", " + Pretty.CompactDuration (calendarEvent.StartTime, calendarEvent.EndTime);
                    }
                    DurationLabel.Text = duration;
                }
                DotView.SetBackgroundResource (Bind.ColorForUser (calendarEvent.GetColorIndex ()));
            }
        }

        class DayHeaderViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            View AddView;
            Android.Widget.TextView NumberLabel;
            Android.Widget.TextView WeekdayLabel;
            Android.Widget.TextView DateLabel;

            EventHandler AddHandler;

            public static DayHeaderViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.CalendarListDayHeaderItem, parent, false);
                return new DayHeaderViewHolder (view);
            }

            public DayHeaderViewHolder (View view) : base (view)
            {
                NumberLabel = view.FindViewById (Resource.Id.number) as Android.Widget.TextView;
                WeekdayLabel = view.FindViewById (Resource.Id.weekday) as Android.Widget.TextView;
                DateLabel = view.FindViewById (Resource.Id.date) as Android.Widget.TextView;
                AddView = view.FindViewById (Resource.Id.add_button);
            }

            public void SetDay (DateTime date)
            {
                NumberLabel.Text = String.Format ("{0}", date.Day);
                WeekdayLabel.Text = date.ToString ("dddd");
                DateLabel.Text = NachoCore.Utils.Pretty.LongMonthDayYear (date);
            }

            public void SetAddHandler (EventHandler addHandler)
            {
                if (AddHandler != null) {
                    AddView.Click -= AddHandler;
                }
                AddHandler = addHandler;
                if (AddHandler != null) {
                    AddView.Click += AddHandler;
                }
            }
        }

    }
}