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

using NachoCore;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class CalendarFragment : Fragment, MainTabsActivity.Tab
    {

        private const int REQUEST_SHOW_EVENT = 1;

        #region Tab Interface

        INcEventProvider Events;

        public bool OnCreateOptionsMenu (MainTabsActivity tabActivity, IMenu menu)
        {
            tabActivity.MenuInflater.Inflate (Resource.Menu.calendar, menu);
            // TODO: Customize today button with today's date
            return true;
        }

        public void OnTabSelected (MainTabsActivity tabActivity)
        {
            tabActivity.ShowActionButton (Resource.Drawable.floating_action_new_event, ActionButtonClicked);
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
                GoToToday ();
                return true;
            }
            return false;
        }

        #endregion

        #region Subviews

        CalendarPagerView PagerView;

        void FindSubviews (View view)
        {
            PagerView = view.FindViewById (Resource.Id.pager) as CalendarPagerView;
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
            return view;
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
            // TODO: scroll to appropriate date
            //var position = eventListAdapter.PositionForDate (date);
            //ScrollToPosition (position);
        }

        bool PagerHasEvents (DateTime date)
        {
            return false;
            //return eventListAdapter.HasEvents (date);
        }

        bool PagerIsSupportedDate (DateTime date)
        {
            return true;
            //return eventListAdapter.IsSupportedDate (date);
        }

        #endregion

        #region User Actions

        void ActionButtonClicked (object sender, EventArgs args)
        {
            ShowNewEvent ();
        }

        #endregion

        #region Private Helpers

        void GoToToday ()
        {
            PagerView.SetHighlightedDate (DateTime.Today);
            // TODO: scroll to date
        }

        void ShowNewEvent ()
        {
            var intent = EventEditActivity.BuildNewEventIntent (Activity);
            StartActivity (intent);
        }

        void ShowEvent (McEvent event_)
        {
            var intent = EventViewActivity.BuildIntent (Activity, event_.Id);
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

        #endregion
    }
}
