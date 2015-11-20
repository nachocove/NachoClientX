using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class HotMessageFragment : Android.App.Fragment, MessageDownloadDelegate
    {
        public event EventHandler<McEmailMessageThread> onMessageClick;

        McEmailMessage message;
        McEmailMessageThread thread;
        NcEmailMessageBundle bundle;

        // These are meaningful only if the message is meeting related
        McMeetingRequest meeting;
        McCalendar calendarItem;

        MessageDownloader messageDownloader;

        Android.Webkit.WebView webView;

        // Display first message of a thread in a cardview
        public static HotMessageFragment newInstance (McEmailMessageThread thread)
        {
            var fragment = new HotMessageFragment ();

            fragment.thread = thread;
            fragment.message = thread.FirstMessageSpecialCase ();

            // Hot query returns single messages for threads so
            // fix up the number of messages in the thread here
            fragment.thread.UpdateThreadCount (fragment.message.ConversationId);

            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.HotMessageFragment, container, false);
            AttachListeners (view);
            BindValues (view);

            return view;
        }

        public override void OnDestroyView ()
        {
            base.OnDestroyView ();
            DetachListeners (View);
        }

        void AttachListeners (View view)
        {
            view.Click += View_Click;

            var replyButton = view.FindViewById (Resource.Id.reply);
            replyButton.Click += ReplyButton_Click;

            var replyAllButton = view.FindViewById (Resource.Id.reply_all);
            replyAllButton.Click += ReplyAllButton_Click;

            var forwardButton = view.FindViewById (Resource.Id.forward);
            forwardButton.Click += ForwardButton_Click;

            var archiveButton = view.FindViewById (Resource.Id.archive);
            archiveButton.Click += ArchiveButton_Click;

            var deleteButton = view.FindViewById (Resource.Id.delete);
            deleteButton.Click += DeleteButton_Click;

            var chiliButton = view.FindViewById (Resource.Id.chili);
            chiliButton.Click += ChiliButton_Click;

            webView = view.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
            webView.SetOnTouchListener (new IgnoreTouchListener (view));
        }

        void DetachListeners (View view)
        {
            view.Click -= View_Click;

            var replyButton = view.FindViewById (Resource.Id.reply);
            replyButton.Click -= ReplyButton_Click;

            var replyAllButton = view.FindViewById (Resource.Id.reply_all);
            replyAllButton.Click -= ReplyAllButton_Click;

            var forwardButton = view.FindViewById (Resource.Id.forward);
            forwardButton.Click -= ForwardButton_Click;

            var archiveButton = view.FindViewById (Resource.Id.archive);
            archiveButton.Click -= ArchiveButton_Click;

            var deleteButton = view.FindViewById (Resource.Id.delete);
            deleteButton.Click -= DeleteButton_Click;

            var chiliButton = view.FindViewById (Resource.Id.chili);
            chiliButton.Click -= ChiliButton_Click;

            webView = view.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
            webView.SetOnTouchListener (null);
        }

        void BindValues (View view)
        {
            Bind.BindMessageHeader (thread, message, view);
            view.FindViewById<TextView> (Resource.Id.subject).SetMaxLines (100);
            BindMeetingRequest (view);
            if (message.BodyId != 0) {
                bundle = new NcEmailMessageBundle (message);
            } else {
                bundle = null;
            }
            if (bundle == null || bundle.NeedsUpdate) {
                messageDownloader = new MessageDownloader ();
                messageDownloader.Bundle = bundle;
                messageDownloader.Delegate = this;
                messageDownloader.Download (message);
            } else {
                RenderBody ();
            }
        }

        void BindMeetingRequest (View view)
        {
            meeting = message.MeetingRequest;

            if (null == meeting) {
                view.FindViewById<View> (Resource.Id.event_in_message).Visibility = ViewStates.Gone;
                return;
            }

            calendarItem = McCalendar.QueryByUID (message.AccountId, meeting.GetUID ());

            var whenView = view.FindViewById<TextView> (Resource.Id.event_when_label);
            whenView.Text = NcEventDetail.GetDateString (meeting);

            var durationView = view.FindViewById<TextView> (Resource.Id.event_duration_label);
            durationView.Text = NcEventDetail.GetDurationString (meeting);

            var recurrenceView = view.FindViewById<TextView> (Resource.Id.event_recurrence_label);
            if (0 == meeting.recurrences.Count) {
                recurrenceView.Visibility = ViewStates.Gone;
            } else {
                recurrenceView.Text = NcEventDetail.GetRecurrenceString (meeting);
            }

            string location = meeting.GetLocation ();
            if (string.IsNullOrEmpty (location)) {
                view.FindViewById<View> (Resource.Id.location_view).Visibility = ViewStates.Gone;
            } else {
                view.FindViewById<TextView> (Resource.Id.event_location_label).Text = location;
            }

            var organizerAddress = NcEmailAddress.ParseMailboxAddressString (meeting.Organizer);
            if (!message.IsMeetingResponse && null != organizerAddress && null != organizerAddress.Address) {
                string email = organizerAddress.Address;
                string name = organizerAddress.Name;
                var organizerEmailLabel = view.FindViewById<TextView> (Resource.Id.event_organizer_email_label);
                organizerEmailLabel.Text = email;
                if (string.IsNullOrEmpty (name)) {
                    foreach (var contact in McContact.QueryByEmailAddress (meeting.AccountId, email)) {
                        if (!string.IsNullOrEmpty (contact.DisplayName)) {
                            name = contact.DisplayName;
                            break;
                        }
                    }
                }
                string initials;
                if (!string.IsNullOrEmpty (name)) {
                    var organizerNameLabel = view.FindViewById<TextView> (Resource.Id.event_organizer_label);
                    organizerNameLabel.Text = name;
                    initials = ContactsHelper.NameToLetters (name);
                } else {
                    initials = ContactsHelper.NameToLetters (email);
                }
                var color = Util.ColorResourceForEmail (email);
                var imageView = view.FindViewById<ContactPhotoView> (Resource.Id.event_organizer_initials);
                imageView.SetEmailAddress (meeting.AccountId, email, initials, color);
            } else {
                view.FindViewById<View> (Resource.Id.event_organizer_view).Visibility = ViewStates.Gone;
            }

            if (!message.IsMeetingRequest) {
                view.FindViewById<View>(Resource.Id.event_attendee_view).Visibility = ViewStates.Gone;
            } else {
                var attendeesFromMessage = NcEmailAddress.ParseAddressListString (Pretty.Join (message.To, message.Cc, ", "));
                for (int a = 0; a < 5; ++a) {
                    var attendeePhotoView = AttendeeInitialsView (view, a);
                    var attendeeNameView = AttendeeNameView (view, a);
                    if (4 == a && 5 < attendeesFromMessage.Count) {
                        attendeePhotoView.SetPortraitId (0, string.Format ("+{0}", attendeesFromMessage.Count - a), Resource.Drawable.UserColor0);
                        attendeeNameView.Text = "";
                    } else if (a < attendeesFromMessage.Count) {
                        var attendee = attendeesFromMessage [a] as MimeKit.MailboxAddress;
                        var initials = ContactsHelper.NameToLetters (attendee.Name);
                        var color = Util.ColorResourceForEmail (attendee.Address);
                        attendeePhotoView.SetEmailAddress (message.AccountId, attendee.Address, initials, color);
                        attendeeNameView.Text = GetFirstName (attendee.Name);
                    } else {
                        attendeePhotoView.Visibility = ViewStates.Gone;
                        attendeeNameView.Visibility = ViewStates.Gone;
                    }
                }
            }

            // The Hot view cards never use the Attend/Maybe/Decline buttons.  They always use the message instead.
            view.FindViewById(Resource.Id.event_rsvp_view).Visibility = ViewStates.Gone;
            view.FindViewById (Resource.Id.event_message_view).Visibility = ViewStates.Visible;
            if (message.IsMeetingResponse) {
                ShowAttendeeResponseBar (view);
            } else if (message.IsMeetingCancelation) {
                ShowCancellationBar (view);
            } else {
                ShowRequestChoicesBar (view);
            }
        }

        void ShowRequestChoicesBar (View view)
        {
            var iconView = view.FindViewById<ImageView> (Resource.Id.event_message_icon);
            var textView = view.FindViewById<TextView> (Resource.Id.event_message_text);

            NcResponseType status = NcResponseType.None;
            if (null != calendarItem && calendarItem.ResponseTypeIsSet) {
                status = calendarItem.ResponseType;
            }
            switch (status) {
            case NcResponseType.Accepted:
                iconView.SetImageResource (Resource.Drawable.event_attend_active);
                textView.Text = "You accepted the meeting.";
                break;
            case NcResponseType.Tentative:
                iconView.SetImageResource (Resource.Drawable.event_maybe_active);
                textView.Text = "You tentatively accepted the meeting.";
                break;
            case NcResponseType.Declined:
                iconView.SetImageResource (Resource.Drawable.event_decline_active);
                textView.Text = "You declined the meeting.";
                break;
            default:
                iconView.Visibility = ViewStates.Gone;
                textView.Text = "You have not yet responded to the meeting.";
                break;
            }
        }

        void ShowCancellationBar (View view)
        {
            var iconView = view.FindViewById<ImageView> (Resource.Id.event_message_icon);
            var textView = view.FindViewById<TextView> (Resource.Id.event_message_text);

            iconView.Visibility = ViewStates.Gone;
            textView.Text = "The meeting has been canceled.";
        }

        void ShowAttendeeResponseBar (View view)
        {
            int iconResourceId;
            string messageFormat;
            switch (message.MeetingResponseValue) {
            case NcResponseType.Accepted:
                iconResourceId = Resource.Drawable.event_attend_active;
                messageFormat = "{0} accepted the meeting.";
                break;
            case NcResponseType.Tentative:
                iconResourceId = Resource.Drawable.event_maybe_active;
                messageFormat = "{0} tentatively accepted the meeting.";
                break;
            case NcResponseType.Declined:
                iconResourceId = Resource.Drawable.event_decline_active;
                messageFormat = "{0} declined the meeting.";
                break;
            default:
                Log.Warn (Log.LOG_CALENDAR, "Unknown meeting response status: {0}", message.MessageClass);
                iconResourceId = 0;
                messageFormat = "The status of {0} is unknown.";
                break;
            }

            string displayName;
            var responder = NcEmailAddress.ParseMailboxAddressString (message.From);
            if (null == responder) {
                displayName = message.From;
            } else if (!string.IsNullOrEmpty (responder.Name)) {
                displayName = responder.Name;
            } else {
                displayName = responder.Address;
            }

            var icon = view.FindViewById<ImageView> (Resource.Id.event_message_icon);
            if (0 == iconResourceId) {
                icon.Visibility = ViewStates.Gone;
            } else {
                icon.SetImageResource (iconResourceId);
            }
            var text = view.FindViewById<TextView> (Resource.Id.event_message_text);
            text.Text = string.Format (messageFormat, displayName);
        }

        private ContactPhotoView AttendeeInitialsView (View parent, int attendeeIndex)
        {
            int id;
            switch (attendeeIndex) {
            case 0:
                id = Resource.Id.event_attendee_0;
                break;
            case 1:
                id = Resource.Id.event_attendee_1;
                break;
            case 2:
                id = Resource.Id.event_attendee_2;
                break;
            case 3:
                id = Resource.Id.event_attendee_3;
                break;
            case 4:
                id = Resource.Id.event_attendee_4;
                break;
            default:
                NcAssert.CaseError (string.Format ("Attendee index {0} is out of range. It must be [0..4]", attendeeIndex));
                return null;
            }
            return parent.FindViewById<ContactPhotoView> (id);
        }

        private TextView AttendeeNameView (View parent, int attendeeIndex)
        {
            int id;
            switch (attendeeIndex) {
            case 0:
                id = Resource.Id.event_attendee_name_0;
                break;
            case 1:
                id = Resource.Id.event_attendee_name_1;
                break;
            case 2:
                id = Resource.Id.event_attendee_name_2;
                break;
            case 3:
                id = Resource.Id.event_attendee_name_3;
                break;
            case 4:
                id = Resource.Id.event_attendee_name_4;
                break;
            default:
                NcAssert.CaseError (string.Format ("Attendee index {0} is out of range. It must be [0..4]", attendeeIndex));
                return null;
            }
            return parent.FindViewById<TextView> (id);
        }

        private static string GetFirstName (string displayName)
        {
            string[] names = displayName.Split (new char [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (names [0] == null) {
                return "";
            }
            if (names [0].Length > 1) {
                return char.ToUpper (names [0] [0]) + names [0].Substring (1);
            }
            return names [0].ToUpper ();
        }
        public void MessageDownloadDidFinish (MessageDownloader downloader)
        {
            bundle = downloader.Bundle;
            RenderBody ();
        }

        public void MessageDownloadDidFail (MessageDownloader downloader, NcResult result)
        {
            // TODO: show this inline, possibly with message preview (if available)
        }

        void RenderBody ()
        {
            if (bundle != null) {
                if (bundle.FullHtmlUrl != null) {
                    webView.LoadUrl (bundle.FullHtmlUrl.AbsoluteUri);
                } else {
                    var html = bundle.FullHtml;
                    webView.LoadDataWithBaseURL (bundle.BaseUrl.AbsoluteUri, html, "text/html", "utf-8", null);
                }
            }
        }

        public class IgnoreTouchListener : Java.Lang.Object, View.IOnTouchListener
        {
            View view;

            public IgnoreTouchListener (View view)
            {
                this.view = view;
            }

            public bool OnTouch (View v, MotionEvent e)
            {
//                view.OnTouchEvent (e);
                return false;
            }
        }

        void DoneWithMessage ()
        {
        }

        void ChiliButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ChiliButton_Click");
            NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
            Bind.BindMessageChili (thread, message, (Android.Widget.ImageView)sender);
        }

        void ArchiveButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ArchiveButton_Click");
            NcEmailArchiver.Archive (message);
            DoneWithMessage ();
        }

        void DeleteButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "DeleteButton_Click");
            NcEmailArchiver.Delete (message);
            DoneWithMessage ();
        }

        void ForwardButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ForwardButton_Click");
            StartComposeActivity (EmailHelper.Action.Forward);
        }

        void ReplyButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ReplyButton_Click");
            StartComposeActivity (EmailHelper.Action.Reply);
        }

        void ReplyAllButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ReplyAllButton_Click");
            StartComposeActivity (EmailHelper.Action.ReplyAll);
        }

        void StartComposeActivity (EmailHelper.Action action)
        {
            StartActivity (MessageComposeActivity.RespondIntent (this.Activity, action, thread.FirstMessageId));
        }

        void View_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "View_Click");
            if (null != onMessageClick) {
                onMessageClick (this, thread);
            }
        }
    }
}

