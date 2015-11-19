﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

using MimeKit;
using Android.Content.PM;
using NachoCore.Brain;

namespace NachoClient.AndroidClient
{
    public interface IMessageViewFragmentOwner
    {
        void DoneWithMessage ();

        McEmailMessage MessageToView { get; }

        McEmailMessageThread ThreadToView { get; }
    }

    public class MessageViewFragment : Fragment, MessageDownloadDelegate
    {
        McEmailMessage message;
        McEmailMessageThread thread;
        NcEmailMessageBundle bundle;

        // These are meaningful only if the message is meeting related
        McMeetingRequest meeting;
        McCalendar calendarItem;
        bool removeFromCalendarEnabled = false;

        ButtonBar buttonBar;

        MessageDownloader messageDownloader;

        Android.Webkit.WebView webView;
        NachoWebViewClient webViewClient;

        public static MessageViewFragment newInstance (McEmailMessageThread thread, McEmailMessage message)
        {
            var fragment = new MessageViewFragment ();
            fragment.message = message;
            fragment.thread = thread;
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.MessageViewFragment, container, false);

            buttonBar = new ButtonBar (view);

            buttonBar.SetIconButton (ButtonBar.Button.Right1, Resource.Drawable.folder_move, SaveButton_Click);

            webView = view.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
            webViewClient = new NachoWebViewClient ();
            webView.SetWebViewClient (webViewClient);

            AttachListeners (view);

            return view;
        }

        public override void OnActivityCreated (Bundle savedInstanceState)
        {
            base.OnActivityCreated (savedInstanceState);

            thread = ((IMessageViewFragmentOwner)Activity).ThreadToView;
            message = ((IMessageViewFragmentOwner)Activity).MessageToView;

            // Refresh message to make sure it's still around
            message = McEmailMessage.QueryById<McEmailMessage>(message.Id);
            if (null == message) {
                Finish ();
                return;
            }
            if (message.BodyId != 0) {
                bundle = new NcEmailMessageBundle (message);
            } else {
                bundle = null;
            }

            NcBrain.MessageReadStatusUpdated (message, DateTime.UtcNow, 0.1);

            var inflater = Activity.LayoutInflater;
            var attachments = McAttachment.QueryByItem (message);
            var attachmentsView = View.FindViewById<LinearLayout> (Resource.Id.attachment_list_view);
            Bind.BindAttachmentListView (attachments, attachmentsView, inflater, AttachmentToggle_Click, AttachmentSelectedCallback, AttachmentErrorCallback);

            // MarkAsRead() will change the message from unread to read only if the body has been
            // completely downloaded, so it is safe to call it unconditionally.  We put the call
            // here, rather than in ConfigureAndLayout(), to handle the case where the body is
            // downloaded long after the message view has been opened.
            EmailHelper.MarkAsRead (thread);
        }

        public override void OnStart ()
        {
            base.OnStart ();

            BindValues (View);
        }

        public override void OnDestroyView ()
        {
            base.OnDestroyView ();
            DetachListeners (View);
        }

        public void AttachmentSelectedCallback (McAttachment attachment)
        {
            if (attachment.IsImageFile ()) {
                var viewerIntent = ImageViewActivity.ImageViewIntent (this.Activity, attachment.GetFileDirectory (), attachment.GetFileName ());
                StartActivity (viewerIntent);
                return;
            }

            // Look for a handler on the system.

            try {
                var myIntent = new Intent (Intent.ActionView);
                var file = new Java.IO.File (attachment.GetFilePath ()); 
                var extension = Android.Webkit.MimeTypeMap.GetFileExtensionFromUrl (Android.Net.Uri.FromFile (file).ToString ());
                var mimetype = Android.Webkit.MimeTypeMap.Singleton.GetMimeTypeFromExtension (extension);
                myIntent.SetDataAndType (Android.Net.Uri.FromFile (file), mimetype);
                var packageManager = this.Activity.PackageManager;
                var activities = packageManager.QueryIntentActivities (myIntent, PackageInfoFlags.MatchDefaultOnly);
                var isIntentSafe = 0 < activities.Count;
                if (isIntentSafe) {
                    StartActivity (myIntent);
                } else {
                    NcAlertView.ShowMessage (Activity, "Attachment", "No application can open this attachment.");
                }
            } catch (Exception e) {
                // TODO: handle exception
                String data = e.Message;
            }
        }

        public  void AttachmentErrorCallback (McAttachment attachment, NcResult nr)
        {
        }

        void AttachmentToggle_Click (object sender, EventArgs e)
        {
            var attachmentsView = View.FindViewById<View> (Resource.Id.attachment_list_view);
            Bind.ToggleAttachmentList (attachmentsView);
        }

        void AttachListeners (View view)
        {
            view.FindViewById (Resource.Id.reply).Click += ReplyButton_Click;
            view.FindViewById (Resource.Id.reply_all).Click += ReplyAllButton_Click;
            view.FindViewById (Resource.Id.forward).Click += ForwardButton_Click;
            view.FindViewById (Resource.Id.archive).Click += ArchiveButton_Click;
            view.FindViewById (Resource.Id.delete).Click += DeleteButton_Click;
            view.FindViewById (Resource.Id.chili).Click += ChiliButton_Click;
            view.FindViewById (Resource.Id.event_attend_button).Click += AttendButton_Click;
            view.FindViewById (Resource.Id.event_maybe_button).Click += MaybeButton_Click;
            view.FindViewById (Resource.Id.event_decline_button).Click += DeclineButton_Click;
            view.FindViewById (Resource.Id.event_organizer_initials).Click += MeetingOrganizer_Click;
            view.FindViewById (Resource.Id.event_attendee_0).Click += Attendees_Click;
            view.FindViewById (Resource.Id.event_attendee_1).Click += Attendees_Click;
            view.FindViewById (Resource.Id.event_attendee_2).Click += Attendees_Click;
            view.FindViewById (Resource.Id.event_attendee_3).Click += Attendees_Click;
            view.FindViewById (Resource.Id.event_attendee_4).Click += Attendees_Click;
        }

        void DetachListeners (View view)
        {
            buttonBar.ClearAllListeners ();

            view.FindViewById (Resource.Id.reply).Click -= ReplyButton_Click;
            view.FindViewById (Resource.Id.reply_all).Click -= ReplyAllButton_Click;
            view.FindViewById (Resource.Id.forward).Click -= ForwardButton_Click;
            view.FindViewById (Resource.Id.archive).Click -= ArchiveButton_Click;
            view.FindViewById (Resource.Id.delete).Click -= DeleteButton_Click;
            view.FindViewById (Resource.Id.chili).Click -= ChiliButton_Click;
            view.FindViewById (Resource.Id.event_attend_button).Click -= AttendButton_Click;
            view.FindViewById (Resource.Id.event_maybe_button).Click -= MaybeButton_Click;
            view.FindViewById (Resource.Id.event_decline_button).Click -= DeclineButton_Click;
            view.FindViewById (Resource.Id.event_organizer_initials).Click -= MeetingOrganizer_Click;
            view.FindViewById (Resource.Id.event_attendee_0).Click -= Attendees_Click;
            view.FindViewById (Resource.Id.event_attendee_1).Click -= Attendees_Click;
            view.FindViewById (Resource.Id.event_attendee_2).Click -= Attendees_Click;
            view.FindViewById (Resource.Id.event_attendee_3).Click -= Attendees_Click;
            view.FindViewById (Resource.Id.event_attendee_4).Click -= Attendees_Click;

            if (removeFromCalendarEnabled) {
                removeFromCalendarEnabled = false;
                view.FindViewById (Resource.Id.event_message_icon).Click -= RemoveFromCalendar_Click;
            }
        }

        void BindValues (View view)
        {
            Bind.BindMessageHeader (null, message, view);
            // The header view is shared between the message list view and the message detail view.
            // In the list view, the subject should be truncated to a single line.  In the detail
            // view, the full subject needs to be shown.
            view.FindViewById<TextView> (Resource.Id.subject).SetMaxLines (100);

            BindMeetingRequest ();

            if (bundle == null || bundle.NeedsUpdate) {
                messageDownloader = new MessageDownloader ();
                messageDownloader.Bundle = bundle;
                messageDownloader.Delegate = this;
                messageDownloader.Download (message);
            } else {
                RenderBody ();
            }
        }

        void BindMeetingRequest ()
        {
            meeting = message.MeetingRequest;

            if (null == meeting) {
                View.FindViewById<View> (Resource.Id.event_in_message).Visibility = ViewStates.Gone;
                return;
            }

            calendarItem = McCalendar.QueryByUID (message.AccountId, meeting.GetUID ());

            var whenView = View.FindViewById<TextView> (Resource.Id.event_when_label);
            whenView.Text = NcEventDetail.GetDateString (meeting);

            var durationView = View.FindViewById<TextView> (Resource.Id.event_duration_label);
            durationView.Text = NcEventDetail.GetDurationString (meeting);

            var recurrenceView = View.FindViewById<TextView> (Resource.Id.event_recurrence_label);
            if (0 == meeting.recurrences.Count) {
                recurrenceView.Visibility = ViewStates.Gone;
            } else {
                recurrenceView.Text = NcEventDetail.GetRecurrenceString (meeting);
            }

            string location = meeting.GetLocation ();
            if (string.IsNullOrEmpty (location)) {
                View.FindViewById<View> (Resource.Id.location_view).Visibility = ViewStates.Gone;
            } else {
                View.FindViewById<TextView> (Resource.Id.event_location_label).Text = location;
            }

            var organizerAddress = NcEmailAddress.ParseMailboxAddressString (meeting.Organizer);
            if (!message.IsMeetingResponse && null != organizerAddress && null != organizerAddress.Address) {
                string email = organizerAddress.Address;
                string name = organizerAddress.Name;
                var organizerEmailLabel = View.FindViewById<TextView> (Resource.Id.event_organizer_email_label);
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
                    var organizerNameLabel = View.FindViewById<TextView> (Resource.Id.event_organizer_label);
                    organizerNameLabel.Text = name;
                    initials = ContactsHelper.NameToLetters (name);
                } else {
                    initials = ContactsHelper.NameToLetters (email);
                }
                var color = Util.ColorResourceForEmail (email);
                var imageView = View.FindViewById<ContactPhotoView> (Resource.Id.event_organizer_initials);
                imageView.SetEmailAddress (meeting.AccountId, email, initials, color);
            } else {
                View.FindViewById<View> (Resource.Id.event_organizer_view).Visibility = ViewStates.Gone;
            }

            if (!message.IsMeetingRequest) {
                View.FindViewById<View>(Resource.Id.event_attendee_view).Visibility = ViewStates.Gone;
            } else {
                var attendeesFromMessage = NcEmailAddress.ParseAddressListString (Pretty.Join (message.To, message.Cc, ", "));
                for (int a = 0; a < 5; ++a) {
                    var attendeePhotoView = AttendeeInitialsView (View, a);
                    var attendeeNameView = AttendeeNameView (View, a);
                    if (4 == a && 5 < attendeesFromMessage.Count) {
                        attendeePhotoView.SetPortraitId (0, string.Format ("+{0}", attendeesFromMessage.Count - a), Resource.Drawable.UserColor0);
                        attendeeNameView.Text = "";
                    } else if (a < attendeesFromMessage.Count) {
                        var attendee = attendeesFromMessage [a] as MailboxAddress;
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

            if (message.IsMeetingResponse) {
                ShowAttendeeResponseBar ();
            } else if (message.IsMeetingCancelation) {
                ShowCancellationBar ();
            } else {
                ShowRequestChoicesBar ();
            }
        }

        void ShowRequestChoicesBar ()
        {
            if (null != calendarItem && calendarItem.ResponseTypeIsSet) {
                switch (calendarItem.ResponseType) {
                case NcResponseType.Accepted:
                    View.FindViewById<ImageView> (Resource.Id.event_attend_button).SetImageResource (Resource.Drawable.event_attend_active);
                    break;
                case NcResponseType.Tentative:
                    View.FindViewById<ImageView> (Resource.Id.event_maybe_button).SetImageResource (Resource.Drawable.event_maybe_active);
                    break;
                case NcResponseType.Declined:
                    View.FindViewById<ImageView> (Resource.Id.event_decline_button).SetImageResource (Resource.Drawable.event_decline_active);
                    break;
                }
            }
        }

        void ShowCancellationBar ()
        {
            View.FindViewById<View> (Resource.Id.event_rsvp_view).Visibility = ViewStates.Gone;
            View.FindViewById<View> (Resource.Id.event_message_view).Visibility = ViewStates.Visible;

            bool eventExists = (null != calendarItem);
            if (eventExists && DateTime.MinValue != message.MeetingRequest.RecurrenceId) {
                var exceptions = McException.QueryForExceptionId (calendarItem.Id, message.MeetingRequest.RecurrenceId);
                eventExists = (0 == exceptions.Count || 0 == exceptions [0].Deleted);
            }

            var iconView = View.FindViewById<ImageView> (Resource.Id.event_message_icon);
            var textView = View.FindViewById<TextView> (Resource.Id.event_message_text);
            if (eventExists) {
                iconView.SetImageResource (Resource.Drawable.event_decline);
                textView.Text = "Remove from calendar";
                if (!removeFromCalendarEnabled) {
                    removeFromCalendarEnabled = true;
                    iconView.Click += RemoveFromCalendar_Click;
                }
            } else {
                iconView.Visibility = ViewStates.Gone;
                textView.Text = "The meeting has been canceled.";
            }
        }

        void ShowAttendeeResponseBar ()
        {
            View.FindViewById<View> (Resource.Id.event_rsvp_view).Visibility = ViewStates.Gone;
            View.FindViewById<View> (Resource.Id.event_message_view).Visibility = ViewStates.Visible;

            int iconResourceId;
            string messageFormat;
            switch (message.MeetingResponseValue) {
            case NcResponseType.Accepted:
                iconResourceId = Resource.Drawable.event_attend_active;
                messageFormat = "{0} has accepted the meeting.";
                break;
            case NcResponseType.Tentative:
                iconResourceId = Resource.Drawable.event_maybe_active;
                messageFormat = "{0} has tentatively accepted the meeting.";
                break;
            case NcResponseType.Declined:
                iconResourceId = Resource.Drawable.event_decline_active;
                messageFormat = "{0} has declined the meeting.";
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

            var icon = View.FindViewById<ImageView> (Resource.Id.event_message_icon);
            if (0 == iconResourceId) {
                icon.Visibility = ViewStates.Gone;
            } else {
                icon.SetImageResource (iconResourceId);
            }
            var text = View.FindViewById<TextView> (Resource.Id.event_message_text);
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

        private void UpdateMeetingStatus (NcResponseType status)
        {
            BackEnd.Instance.RespondEmailCmd (message.AccountId, message.Id, status);

            var organizerAddress = NcEmailAddress.ParseMailboxAddressString (meeting.Organizer);
            if (null != organizerAddress && null != organizerAddress.Address && meeting.ResponseRequestedIsSet && meeting.ResponseRequested) {
                var iCalPart = CalendarHelper.MimeResponseFromEmail (meeting, status, message.Subject, meeting.RecurrenceId);
                var mimeBody = CalendarHelper.CreateMime ("", iCalPart, new List<McAttachment> ());
                CalendarHelper.SendMeetingResponse (
                    McAccount.QueryById<McAccount>(message.AccountId), organizerAddress, message.Subject, null, mimeBody, status);
            }
        }

        public void MessageDownloadDidFinish (MessageDownloader downloader)
        {
            bundle = downloader.Bundle;
            RenderBody ();
        }

        public void MessageDownloadDidFail (MessageDownloader downloader, NcResult result)
        {
            // TODO: show this inline, possibly with message preview (if available)
            // and give the user an option to retry if appropriate
            NcAlertView.ShowMessage (Activity, "Could not download message", "Sorry, we were unable to download the message.");
        }

        void RenderBody ()
        {
            if (bundle != null) {
                if (bundle.FullHtmlUrl != null) {
                    Log.Info (Log.LOG_UI, "{0} LoadUrl called", DateTime.Now.MillisecondsSinceEpoch());
                    webView.LoadUrl (bundle.FullHtmlUrl.AbsoluteUri);
                    Log.Info (Log.LOG_UI, "{0} LoadUrl returned", DateTime.Now.MillisecondsSinceEpoch());
                } else {
                    var html = bundle.FullHtml;
                    webView.LoadDataWithBaseURL (bundle.BaseUrl.AbsoluteUri, html, "text/html", "utf-8", null);
                }
            }
            EmailHelper.MarkAsRead (thread);
        }

        void Finish ()
        {
            ((IMessageViewFragmentOwner)Activity).DoneWithMessage ();
        }

        void SaveButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "SaveButton_Click");
            ShowFolderChooser ();
        }

        void ChiliButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ChiliButton_Click");
            NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
            Bind.BindMessageChili (message, (Android.Widget.ImageView)sender);
        }

        void ArchiveButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ArchiveButton_Click");
            NcEmailArchiver.Archive (message);
            Finish ();
        }

        void DeleteButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "DeleteButton_Click");
            NcEmailArchiver.Delete (message);
            Finish ();
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

        void AttendButton_Click (object sender, EventArgs e)
        {
            UpdateMeetingStatus (NcResponseType.Accepted);
            Finish ();
        }

        void MaybeButton_Click (object sender, EventArgs e)
        {
            UpdateMeetingStatus (NcResponseType.Tentative);
            Finish ();
        }

        void DeclineButton_Click (object sender, EventArgs e)
        {
            UpdateMeetingStatus (NcResponseType.Declined);
            Finish ();
        }

        void RemoveFromCalendar_Click (object sender, EventArgs e)
        {
            if (DateTime.MinValue == meeting.RecurrenceId) {
                BackEnd.Instance.DeleteCalCmd (calendarItem.AccountId, calendarItem.Id);
            } else {
                CalendarHelper.CancelOccurrence (calendarItem, meeting.RecurrenceId);
                BackEnd.Instance.UpdateCalCmd (calendarItem.AccountId, calendarItem.Id, sendBody: false);
            }
            Finish ();
        }

        void MeetingOrganizer_Click (object sender, EventArgs e)
        {
            var email = NcEmailAddress.ParseMailboxAddressString (meeting.Organizer).Address;
            var contact = McContact.QueryByEmailAddress (meeting.AccountId, email).FirstOrDefault ();
            if (null != contact) {
                StartActivity (ContactViewActivity.ShowContactIntent (this.Activity, contact));
            }
        }

        void Attendees_Click (object sender, EventArgs e)
        {
            var attendees = new List<McAttendee> ();
            if (!string.IsNullOrEmpty (message.To)) {
                foreach (var attendee in NcEmailAddress.ParseAddressListString(message.To)) {
                    attendees.Add (new McAttendee (message.AccountId, attendee.Name, ((MailboxAddress)attendee).Address, NcAttendeeType.Required));
                }
            }
            if (!string.IsNullOrEmpty(message.Cc)) {
                foreach (var attendee in NcEmailAddress.ParseAddressListString(message.Cc)) {
                    attendees.Add (new McAttendee (message.AccountId, attendee.Name, ((MailboxAddress)attendee).Address, NcAttendeeType.Optional));
                }
            }
            StartActivity (AttendeeViewActivity.AttendeeViewIntent (this.Activity, message.AccountId, attendees));
        }

        void StartComposeActivity (EmailHelper.Action action)
        {
            StartActivity (MessageComposeActivity.RespondIntent (this.Activity, action, message.Id));
        }

        public void ShowFolderChooser ()
        {
            Log.Info (Log.LOG_UI, "ShowFolderChooser: {0}", message);
            var folderFragment = ChooseFolderFragment.newInstance (null);
            folderFragment.SetOnFolderSelected (OnFolderSelected);
            folderFragment.Show (FragmentManager, "ChooseFolderFragment");
        }

        public void OnFolderSelected (McFolder folder, McEmailMessageThread thread)
        {
            Log.Info (Log.LOG_UI, "OnFolderSelected: {0}", message);
            NcEmailArchiver.Move (message, folder);
            Finish ();
        }

    }

    public class AttachmentListViewAdapter : Android.Widget.BaseAdapter<object>
    {
        List<McAttachment> attachmentList;

        public AttachmentListViewAdapter (List<McAttachment> attachmentList)
        {
            this.attachmentList = attachmentList;
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override int Count {
            get {
                return attachmentList.Count;
            }
        }

        public override object this [int position] {  
            get {
                return attachmentList [position];
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.AttachmentListViewCell, parent, false);
            }
            var info = attachmentList [position];

            return view;
        }

    }

    public class NachoWebViewClient : Android.Webkit.WebViewClient
    {
        //        public override void OnReceivedError (Android.Webkit.WebView view, Android.Webkit.ClientError errorCode, string description, string failingUrl)
        //        {
        //            base.OnReceivedError (view, errorCode, description, failingUrl);
        //            Log.Info (Log.LOG_UI, "OnReceivedError: {0}: {1} {2}", failingUrl, errorCode, description);
        //        }
        //
        //        public override Android.Webkit.WebResourceResponse ShouldInterceptRequest (Android.Webkit.WebView view, Android.Webkit.IWebResourceRequest request)
        //        {
        //            Log.Info (Log.LOG_UI, "ShouldInterceptRequest: {1} {0}", request.Url, request.Method);
        //            return base.ShouldInterceptRequest (view, request);
        //        }

        public override void OnPageFinished (Android.Webkit.WebView view, string url)
        {
            Log.Info (Log.LOG_UI, "{0} OnPageFinished", DateTime.Now.MillisecondsSinceEpoch());
        }

        public override bool ShouldOverrideUrlLoading (Android.Webkit.WebView view, string url)
        {
            if (null == url) {
                return false;
            }
            try {
                var uri = Android.Net.Uri.Parse (url);
                var norm = uri.NormalizeScheme ();
                var scheme = norm.Scheme;
                if ("http" == scheme || "https" == scheme) {
                    view.Context.StartActivity (new Intent (Intent.ActionView, Android.Net.Uri.Parse (url)));
                    return true;
                }
            } catch (Exception ex) {
                Log.Info (Log.LOG_UI, "ShouldOverrideUrl: {0}", ex);
            }
            return false;
        }
    }
}
