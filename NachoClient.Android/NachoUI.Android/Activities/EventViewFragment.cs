
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
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class EventViewFragment : Fragment
    {
        McEvent ev;

        public static EventViewFragment newInstance (McEvent ev)
        {
            var fragment = new EventViewFragment ();
            fragment.ev = ev;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.EventViewFragment, container, false);

            BindEventView (ev, view);
            return view;
        }

        ViewStates VisibleIfTrue (bool b)
        {
            return b ? ViewStates.Visible : ViewStates.Gone;
        }

        ViewStates GoneIfNullOrEmpty (string s)
        {
            return VisibleIfTrue (!String.IsNullOrEmpty (s));
        }

        // TODO: Attendees
        // TODO: Attachments
        // TODO: Edit notes view
        // TODO: Change reminder arrow
        void BindEventView (McEvent ev, View view)
        {
            var detail = new NcEventDetail (ev);

            ConfigureRsvpBar (detail, view);

            var titleView = view.FindViewById<TextView> (Resource.Id.event_title);
            titleView.Text = detail.SpecificItem.GetSubject ();

            var whenView = view.FindViewById<TextView> (Resource.Id.event_when_label);
            var durationView = view.FindViewById<TextView> (Resource.Id.event_duration_label);
            var recurrenceView = view.FindViewById<TextView> (Resource.Id.event_recurrence_label);

            whenView.Text = detail.DateString;
            durationView.Text = detail.DurationString;

            recurrenceView.Visibility = VisibleIfTrue (detail.IsRecurring);
            if (detail.IsRecurring) {
                recurrenceView.Text = detail.RecurrenceString;
            }

            var location = detail.SpecificItem.GetLocation ();
            var locationView = view.FindViewById<View> (Resource.Id.location_view);
            locationView.Visibility = GoneIfNullOrEmpty (location);
            if (!String.IsNullOrEmpty (location)) {
                var locationLabel = view.FindViewById<TextView> (Resource.Id.event_location_label);
                locationLabel.Text = location;
            }

            var webview = view.FindViewById<Android.Webkit.WebView> (Resource.Id.event_description_webview);
            var body = McBody.QueryById<McBody> (detail.SpecificItem.BodyId);
            if (McBody.IsNontruncatedBodyComplete (body)) {
                var bodyRenderer = new BodyRenderer ();
                bodyRenderer.Start (webview, body, detail.SpecificItem.NativeBodyType);
                var webClient = new NachoWebViewClient ();
                webview.SetWebViewClient (webClient);
            } else {
                webview.Visibility = ViewStates.Gone;
            }

            var reminderView = view.FindViewById<TextView> (Resource.Id.event_reminder_label);
            reminderView.Text = detail.ReminderString;

            var organizer_view = view.FindViewById<View> (Resource.Id.event_organizer_view);
            organizer_view.Visibility = VisibleIfTrue (detail.HasNonSelfOrganizer);
            if (detail.HasNonSelfOrganizer) {
                var organizerName = detail.SeriesItem.OrganizerName;
                var organizerNameLabel = view.FindViewById<TextView> (Resource.Id.event_organizer_label);
                organizerNameLabel.Visibility = GoneIfNullOrEmpty (organizerName);
                if (!String.IsNullOrEmpty (organizerName)) {
                    organizerNameLabel.Text = organizerName;
                }
                var organizerEmailLabel = view.FindViewById<TextView> (Resource.Id.event_organizer_email_label);
                organizerEmailLabel.Text = detail.SeriesItem.OrganizerEmail;
                var initialsView = view.FindViewById<TextView> (Resource.Id.event_organizer_initials);
                if (String.IsNullOrEmpty (detail.SeriesItem.OrganizerName)) {
                    initialsView.Text = ContactsHelper.NameToLetters (detail.SeriesItem.OrganizerName);
                } else {
                    initialsView.Text = ContactsHelper.NameToLetters (detail.SeriesItem.OrganizerEmail);
                }
            }

            var notesLabel = view.FindViewById<TextView> (Resource.Id.event_notes_label);
            var note = McNote.QueryByTypeId (detail.SeriesItem.Id, McNote.NoteType.Event).FirstOrDefault ();
            if (null == note) {
                notesLabel.Text = "";
            } else {
                notesLabel.Text = note.noteContent;
            }

            var calendarView = view.FindViewById<TextView> (Resource.Id.event_calendar_label);
            calendarView.Text = detail.CalendarNameString;

            var cancelView = view.FindViewById<View> (Resource.Id.event_cancel_view);
            var cancelSeparator = view.FindViewById<View> (Resource.Id.event_cancel_separator);
            cancelView.Visibility = VisibleIfTrue (detail.ShowCancelMeetingButton);
            cancelSeparator.Visibility = VisibleIfTrue (detail.ShowCancelMeetingButton);

        }


        public void ConfigureRsvpBar (NcEventDetail detail, View view)
        {
            var rsvpView = view.FindViewById<View> (Resource.Id.event_rsvp_view);
            var removeView = view.FindViewById<View> (Resource.Id.event_remove_view);
            var cancelledView = view.FindViewById<View> (Resource.Id.event_cancelled_view);
            var organizerView = view.FindViewById<View> (Resource.Id.event_self_organizer_view);
            var separatorView = view.FindViewById<View> (Resource.Id.event_top_separator);

            rsvpView.Visibility = ViewStates.Gone;
            removeView.Visibility = ViewStates.Gone;
            cancelledView.Visibility = ViewStates.Gone;
            organizerView.Visibility = ViewStates.Gone;
            separatorView.Visibility = ViewStates.Visible;

            if (detail.SpecificItem.MeetingStatus == NcMeetingStatus.MeetingAttendeeCancelled) {
                if (detail.Account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter)) {
                    // Show "Remove from calendar"
                    removeView.Visibility = ViewStates.Visible;
                } else {
                    // The calendar is not writable, so the "Remove from calendar" button doesn't make sense.
                    // Instead, just show a message about the meeting being cancelled.
                    cancelledView.Visibility = ViewStates.Visible;
                }
            } else if (detail.IsAppointment) {
                // It's an appointment.  Leave out the header section entirely.
                separatorView.Visibility = ViewStates.Gone;
            } else if (detail.IsOrganizer) {
                // Show "You are the organizer"
                organizerView.Visibility = ViewStates.Visible;
            } else {
                // Show the Attend, Maybe, and Decline buttons.
                rsvpView.Visibility = ViewStates.Visible;
            }
        }
    }
}

