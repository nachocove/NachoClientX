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
using Android.Views;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity ()]
    public class EventViewActivity : NcActivity
    {

        // TODO: accept/reject/maybe

        public const string ACTION_DELETE = "NachoClient.AndroidClient.EventViewActivity.ACTION_DELETE";
        public const string EXTRA_EVENT_ID = "NachoClient.AndroidClient.EventViewActivity.EXTRA_EVENT_ID";
        public const string EXTRA_ANDROID_EVENT_ID = "NachoClient.AndroidClient.EventViewActivity.EXTRA_ANDROID_EVENT_ID";
        public const int REQUEST_EDIT_EVENT = 1;

        McEvent Event;
        bool CanEditEvent;

        #region Intents

        public static Intent BuildIntent (Context context, McEvent calendarEvent)
        {
            if (calendarEvent.DeviceEventId != 0) {
                return BuildAndroidEventIntent (context, calendarEvent.DeviceEventId);
            }
            return BuildIntent (context, calendarEvent.Id);
        }

        public static Intent BuildIntent (Context context, int eventId)
        {
            var intent = new Intent (context, typeof (EventViewActivity));
            intent.PutExtra (EXTRA_EVENT_ID, eventId);
            return intent;
        }

        public static Intent BuildAndroidEventIntent (Context context, long androidEventId)
        {
            var intent = new Intent (context, typeof (EventViewActivity));
            intent.PutExtra (EXTRA_ANDROID_EVENT_ID, androidEventId);
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;
        FloatingActionButton EditActionButton;
        FloatingActionButton AcceptButton;
        FloatingActionButton DeclineButton;
        FloatingActionButton TentativeButton;
        EventViewFragment EventViewFragment;
        View ResponseActionGroup;

        void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
            EditActionButton = FindViewById (Resource.Id.fab) as FloatingActionButton;
            ResponseActionGroup = FindViewById (Resource.Id.response_actions);
            AcceptButton = FindViewById (Resource.Id.accept) as FloatingActionButton;
            DeclineButton = FindViewById (Resource.Id.decline) as FloatingActionButton;
            TentativeButton = FindViewById (Resource.Id.tentative) as FloatingActionButton;

            EditActionButton.Click += EditButtonClicked;
            AcceptButton.Click += AcceptButtonClicked;
            DeclineButton.Click += DeclineButtonClicked;
            TentativeButton.Click += TentativeButtonClicked;
        }

        void ClearSubviews ()
        {

            EditActionButton.Click -= EditButtonClicked;
            AcceptButton.Click -= AcceptButtonClicked;
            DeclineButton.Click -= DeclineButtonClicked;
            TentativeButton.Click -= TentativeButtonClicked;

            Toolbar = null;
            EditActionButton = null;
            EventViewFragment = null;
            ResponseActionGroup = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle savedInstanceState)
        {
            PopulateFromIntent ();
            base.OnCreate (savedInstanceState);
            SetContentView (Resource.Layout.EventViewActivity);
            FindSubviews ();
            Toolbar.Title = "";
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
            if (!CanEditEvent) {
                EditActionButton.Hide ();
                if (Event.IsResponseRequested) {
                    ResponseActionGroup.Visibility = ViewStates.Visible;
                }
            }
        }

        void PopulateFromIntent ()
        {
        	var bundle = Intent.Extras;
            if (Intent.HasExtra (EXTRA_EVENT_ID)) {
                var eventId = bundle.GetInt (EXTRA_EVENT_ID);
                Event = McEvent.QueryById<McEvent> (eventId);
            } else {
                var androidEventId = bundle.GetLong (EXTRA_ANDROID_EVENT_ID);
                Event = NachoPlatform.AndroidCalendars.GetEvent (androidEventId);
            }
            CanEditEvent = CalendarHelper.CanEdit (Event);
        }

        public override void OnAttachFragment (Fragment fragment)
        {
        	base.OnAttachFragment (fragment);
            if (fragment is EventViewFragment) {
                EventViewFragment = (fragment as EventViewFragment);
                EventViewFragment.Event = Event;
                EventViewFragment.CanEditEvent = CanEditEvent;
            }
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            switch (requestCode) {
            case REQUEST_EDIT_EVENT:
                HandleEditComplete (resultCode, data);
                break;
            default:
                base.OnActivityResult (requestCode, resultCode, data);
                break;
            }
        }

        #endregion

        #region Options Menu

        public override bool OnCreateOptionsMenu (IMenu menu)
        {
            MenuInflater.Inflate (Resource.Menu.event_view, menu);
            if (!CanEditEvent) {
                var deleteItem = menu.FindItem (Resource.Id.delete);
                deleteItem.SetVisible (false);
            }
            if (!CalendarHelper.CanCancel (Event)) {
                var cancelItem = menu.FindItem (Resource.Id.cancel);
                cancelItem.SetVisible (false);
            }
            return base.OnCreateOptionsMenu (menu);
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            switch (item.ItemId) {
            case Android.Resource.Id.Home:
                Finish ();
                return true;
            case Resource.Id.delete:
                ShowDeleteConfirmation ();
                return true;
            case Resource.Id.cancel:
                ShowCancelConfirmation ();
                return true;
            case Resource.Id.forward:
                ShowForward ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        #region User Actions

        void EditButtonClicked (object sender, EventArgs e)
        {
            ShowEdit ();
        }

        void AcceptButtonClicked (object sender, EventArgs e)
        {
            SendAcceptResponse ();
        }

        void DeclineButtonClicked (object sender, EventArgs e)
        {
            SendDeclineResponse ();
        }

        void TentativeButtonClicked (object sender, EventArgs e)
        {
            SendTentativeResponse ();
        }

        #endregion

        #region View Updates

        void HandleEditComplete (Result resultCode, Intent data)
        {
            if (resultCode == Result.Ok) {
                if (data != null && data.Action == EventEditActivity.ACTION_DELETE) {
                    FinishWithDeleteAction ();
                } else {
                    Event = McEvent.QueryById<McEvent> (Event.Id);
                    Update ();
                }
            }
        }

        void Update ()
        {
            EventViewFragment.Event = Event;
            EventViewFragment.Update ();
        }

        #endregion

        #region Private Helpers

        void ShowEdit ()
        {
            var intent = EventEditActivity.BuildIntent (this, Event.Id);
            StartActivityForResult (intent, REQUEST_EDIT_EVENT);
        }

        void ShowDeleteConfirmation ()
        {
            var builder = new AlertDialog.Builder (this);
            builder.SetTitle (Resource.String.event_delete_confirmation_message);
            var items = new string [] {
                GetString (Resource.String.event_delete_confirmation_accept)
            };
            builder.SetItems (items, (sender, e) => {
                switch (e.Which) {
                case 0:
                    DeleteEvent ();
                    break;
                default:
                    break;
                }
            });
            builder.Show ();
        }

        void ShowCancelConfirmation ()
        {
        	var builder = new AlertDialog.Builder (this);
            builder.SetTitle (Resource.String.event_cancel_confirmation_message);
        	var items = new string [] {
        				GetString (Resource.String.event_cancel_confirmation_accept)
        			};
        	builder.SetItems (items, (sender, e) => {
        		switch (e.Which) {
        		case 0:
        			CancelOccurrence ();
        			break;
        		default:
        			break;
        		}
        	});
        	builder.Show ();
        }

        void ShowForward ()
        {
            var calendarItem = Event.Calendar;
            if (calendarItem != null) {
                var account = McAccount.EmailAccountForCalendar (calendarItem);
                var subject = EmailHelper.CreateInitialSubjectLine (EmailHelper.Action.Forward, calendarItem.Subject);
                var message = McEmailMessage.MessageWithSubject (account, subject);
                var intent = MessageComposeActivity.ForwardCalendarIntent (this, calendarItem.Id, message);
                StartActivity (intent);
            }
        }

        void DeleteEvent ()
        {
            CalendarHelper.DeleteEvent (Event);
            FinishWithDeleteAction ();
        }

        void CancelOccurrence ()
        {
            CalendarHelper.CancelOccurrence (Event);
        }

        void FinishWithDeleteAction ()
        {
            var intent = new Intent (ACTION_DELETE);
			SetResult (Result.Ok, intent);
            Finish ();
        }

        void SendAcceptResponse ()
        {
            SendResponse (NcResponseType.Accepted);
        }

        void SendDeclineResponse ()
        {
            SendResponse (NcResponseType.Declined);
        }

        void SendTentativeResponse ()
        {
            SendResponse (NcResponseType.Tentative);
        }

        void SendResponse (NcResponseType response)
        {
            if (Event.IsRecurring) {
                ShowResponseConfirmation (response);
            } else {
                CalendarHelper.SendMeetingResponse (Event, response, false);
            }
        }

        void ShowResponseConfirmation (NcResponseType response)
        {
        	var builder = new AlertDialog.Builder (this);
            builder.SetTitle (Resource.String.event_response_confirmation_message);
            var all_format = GetString (Resource.String.event_response_confirmation_all_format);
            var occurrence_format = GetString (Resource.String.event_response_confirmation_occurrence_format);
            string action = "";
            switch (response) {
            case NcResponseType.Accepted:
                action = GetString (Resource.String.event_response_confirmation_accept);
                break;
            case NcResponseType.Declined:
                action = GetString (Resource.String.event_response_confirmation_decline);
                break;
            case NcResponseType.Tentative:
                action = GetString (Resource.String.event_response_confirmation_tentative);
                break;
            }
            var items = new string [] {
                String.Format (all_format, action),
                String.Format (occurrence_format, action)
            };
        	builder.SetItems (items, (sender, e) => {
        		switch (e.Which) {
        		case 0:
                    CalendarHelper.SendMeetingResponse (Event, response, false);
        			break;
                case 1:
                    CalendarHelper.SendMeetingResponse (Event, response, true);
                    break;
        		default:
        			break;
        		}
        	});
        	builder.Show ();
        }

        #endregion

    }
}
