//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using NachoCore.Brain;
using NachoCore;
using Android.Support.V7.Widget;
using Android.Content;
using NachoPlatform;

namespace NachoClient.AndroidClient
{

    public class MessageListAdapter : RecyclerView.Adapter, MessageDownloadDelegate
    {
        public const int LISTVIEW_STYLE = 0;
        public const int CARDVIEW_STYLE = 1;
        public const int SUMMARY_STYLE = 2;

        public event EventHandler<int> onMessageClick;

        public int currentStyle;
        MessageListFragment owner;

        bool searching;

        public MessageListAdapter (MessageListFragment owner, int style)
        {
            this.owner = owner;
            currentStyle = style;
            HasStableIds = true;
        }

        public void StartSearch ()
        {
            searching = true;
            NotifyDataSetChanged ();
        }

        public void CancelSearch ()
        {
            if (searching) {
                searching = false;
                NotifyDataSetChanged ();
            }
        }

        public void RefreshSearchMatches ()
        {
            NotifyDataSetChanged ();
        }

        public bool IsFooterPosition (int position)
        {
            if (!searching) {
                if (CARDVIEW_STYLE == currentStyle) {
                    return (ItemCount == (position + 1));
                }
            }
            return false;
        }

        public override int GetItemViewType (int position)
        {
            if (IsFooterPosition (position)) {
                return SUMMARY_STYLE;
            } else {
                return currentStyle;
            }
        }

        public class MessageViewHolder : RecyclerView.ViewHolder
        {
            public MessageViewHolder (View itemView, Action<int> click) : base (itemView)
            {
                itemView.Click += (object sender, EventArgs e) => click (AdapterPosition);
            }
        }

        public class SummaryViewHolder : RecyclerView.ViewHolder
        {
            public TextView inboxMessageCountView;
            public TextView deferredMessageCountView;
            public TextView deadlinesMessageCountView;

            public SummaryViewHolder (View itemView, Action inboxClick, Action deferredClick, Action deadlinesClick) : base (itemView)
            {
                var inboxView = itemView.FindViewById<View> (Resource.Id.go_to_inbox);
                var deferredView = itemView.FindViewById<View> (Resource.Id.go_to_deferred);
                var deadlinesView = itemView.FindViewById<View> (Resource.Id.go_to_deadlines);

                inboxView.Click += (object sender, EventArgs e) => inboxClick ();
                deferredView.Click += (object sender, EventArgs e) => deferredClick ();
                deadlinesView.Click += (object sender, EventArgs e) => deadlinesClick ();

                inboxMessageCountView = itemView.FindViewById<TextView> (Resource.Id.inbox_message_count);
                deferredMessageCountView = itemView.FindViewById<TextView> (Resource.Id.deferred_message_count);
                deadlinesMessageCountView = itemView.FindViewById<TextView> (Resource.Id.deadlines_message_count);
            }
        }

        public McEmailMessageThread this [int position] {  
            get { 
                if (searching) {
                    return owner.searchResultsMessages.GetEmailThread (position);
                } else {
                    return owner.messages.GetEmailThread (position);
                }
            }
        }

        public override long GetItemId (int position)
        {
            if (IsFooterPosition (position)) {
                return 0; // No item has 0 Id
            }
            if (searching) {
                return owner.searchResultsMessages.GetEmailThread (position).FirstMessageId;
            } else {
                return owner.messages.GetEmailThread (position).FirstMessageId;
            }
        }

        public override int ItemCount {
            get {
                if (searching) {
                    return owner.searchResultsMessages.Count ();
                } else {
                    if (CARDVIEW_STYLE == currentStyle) { // !searching
                        return owner.messages.Count () + 1; // for footer
                    } else {
                        return owner.messages.Count ();
                    }
                }
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            switch (viewType) {
            case LISTVIEW_STYLE:
                return CreateListViewHolder (parent);
            case CARDVIEW_STYLE:
                return CreateCardViewHolder (parent);
            case SUMMARY_STYLE:
                return CreateSummaryViewHolder (parent);
            default:
                return null;
            }
        }

        RecyclerView.ViewHolder CreateSummaryViewHolder (ViewGroup parent)
        {
            var itemView = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.HotSummaryFragment, parent, false);
            itemView.SetMinimumHeight (parent.MeasuredHeight);
            itemView.LayoutParameters.Height = parent.MeasuredHeight;
            return new SummaryViewHolder (itemView, InboxView_Click, DeferredView_Click, DeadlinesView_Click);
        }

        RecyclerView.ViewHolder CreateListViewHolder (ViewGroup parent)
        {
            var itemView = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageCell, parent, false);
            var chiliView = itemView.FindViewById<Android.Widget.ImageView> (Resource.Id.chili);
            chiliView.Click += ChiliView_Click;
            return new MessageViewHolder (itemView, ItemView_Click);
        }

        RecyclerView.ViewHolder CreateCardViewHolder (ViewGroup parent)
        {
            var itemView = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageCard, parent, false);
            AttachCardListeners (itemView);

            var webView = itemView.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);

            webView.Clickable = false;
            webView.LongClickable = false;
            webView.Focusable = false;
            webView.FocusableInTouchMode = false;
            webView.SetOnTouchListener (new IgnoreTouchListener ());

            itemView.SetMinimumHeight (parent.MeasuredHeight);
            itemView.LayoutParameters.Height = parent.MeasuredHeight;
            return new MessageViewHolder (itemView, ItemView_Click);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            BindMessageViewHolder (holder, position);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position, System.Collections.Generic.IList<Java.Lang.Object> payloads)
        {
            BindMessageViewHolder (holder, position);
        }

        void BindMessageViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            switch (GetItemViewType (position)) {
            case LISTVIEW_STYLE:
                BindListView (holder.ItemView, position);
                break;
            case CARDVIEW_STYLE:
                BindCardView (holder.ItemView, position);
                break;
            case SUMMARY_STYLE:
                BindSummaryViewHolder (holder);
                break;
            default:
                NcAssert.CaseError ();
                break;
            }
        }

        View BindListView (View view, int position)
        {
            McEmailMessageThread thread;
            McEmailMessage message;
            if (searching) {
                thread = owner.searchResultsMessages.GetEmailThread (position);
                message = thread.FirstMessageSpecialCase ();
            } else {
                thread = owner.messages.GetEmailThread (position);
                message = owner.GetCachedMessage (position);
            }
            var isDraft = owner.messages.HasDraftsSemantics () || owner.messages.HasOutboxSemantics ();
            Bind.BindMessageHeader (thread, message, view, isDraft);

            NcBrain.MessageNotificationStatusUpdated (message, DateTime.UtcNow, 60);

            // Preview label view                
            var previewView = view.FindViewById<Android.Widget.TextView> (Resource.Id.preview);
            if (null == message) {
                previewView.Text = "";
            } else {
                var cookedPreview = EmailHelper.AdjustPreviewText (message.GetBodyPreviewOrEmpty ());
                previewView.SetText (Android.Text.Html.FromHtml (cookedPreview), Android.Widget.TextView.BufferType.Spannable);
            }

            var multiSelectView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.selected);
            if (owner.multiSelectActive) {
                multiSelectView.Visibility = ViewStates.Visible;
                if (owner.MultiSelectSet.Contains (position)) {
                    multiSelectView.SetImageResource (Resource.Drawable.gen_checkbox_checked);
                } else {
                    multiSelectView.SetImageResource (Resource.Drawable.gen_checkbox);
                }
            } else {
                multiSelectView.Visibility = ViewStates.Invisible;
            }

            var chiliTagView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.chili);
            chiliTagView.Tag = position;

            return view;
        }

        void MessageFromSender (object sender, out McEmailMessageThread thread, out McEmailMessage message)
        {
            var view = (View)sender;
            var position = (int)view.Tag;
            if (searching) {
                thread = owner.searchResultsMessages.GetEmailThread (position);
            } else {
                thread = owner.messages.GetEmailThread (position);
            }
            message = thread.FirstMessageSpecialCase ();
        }

        void ItemView_Click (int position)
        {
            if (null != onMessageClick) {
                onMessageClick (this, position);
            }
        }

        void ChiliView_Click (object sender, EventArgs e)
        {
            McEmailMessage message;
            McEmailMessageThread thread;
            var chiliView = (Android.Widget.ImageView)sender;
            MessageFromSender (sender, out thread, out message);
            NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
            Bind.BindMessageChili (thread, message, chiliView);
        }

        class MessageDownloaderWithWebView : MessageDownloader
        {
            public WebView webView;
        }

        View BindCardView (View view, int position)
        {
            var thread = owner.messages.GetEmailThread (position);
            var message = owner.GetCachedMessage (position);

            BindValues (view, thread, message);

            view.FindViewById (Resource.Id.chili).Tag = position;
            view.FindViewById (Resource.Id.reply).Tag = position;
            view.FindViewById (Resource.Id.reply_all).Tag = position;
            view.FindViewById (Resource.Id.forward).Tag = position;
            view.FindViewById (Resource.Id.archive).Tag = position;
            view.FindViewById (Resource.Id.delete).Tag = position;

            return view;
        }

        public class IgnoreTouchListener : Java.Lang.Object, View.IOnTouchListener
        {
            public bool OnTouch (View v, MotionEvent e)
            {
                return false;
            }
        }

        void AttachCardListeners (View view)
        {
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

            var chiliView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.chili);
            chiliView.Click += ChiliView_Click;
        }

        void BindValues (View view, McEmailMessageThread thread, McEmailMessage message)
        {
            Bind.BindMessageHeader (thread, message, view);
            view.FindViewById<TextView> (Resource.Id.subject).SetMaxLines (100);
            BindMeetingRequest (view, message);
            var webView = view.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
            NcEmailMessageBundle bundle;
            if (message.BodyId != 0) {
                bundle = new NcEmailMessageBundle (message);
            } else {
                bundle = null;
            }
            if (bundle == null || bundle.NeedsUpdate) {
                var messageDownloader = new MessageDownloaderWithWebView ();
                messageDownloader.webView = webView;
                messageDownloader.Bundle = bundle;
                messageDownloader.Delegate = this;
                messageDownloader.Download (message);
            } else {
                RenderBody (webView, bundle);
            }
        }

        void BindMeetingRequest (View view, McEmailMessage message)
        {
            var meeting = message.MeetingRequest;

            if (null == meeting) {
                view.FindViewById<View> (Resource.Id.event_in_message).Visibility = ViewStates.Gone;
                return;
            }

            var calendarItem = McCalendar.QueryByUID (message.AccountId, meeting.GetUID ());

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
                view.FindViewById<View> (Resource.Id.event_attendee_view).Visibility = ViewStates.Gone;
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
            view.FindViewById (Resource.Id.event_rsvp_view).Visibility = ViewStates.Gone;
            view.FindViewById (Resource.Id.event_message_view).Visibility = ViewStates.Visible;
            if (message.IsMeetingResponse) {
                ShowAttendeeResponseBar (view, message);
            } else if (message.IsMeetingCancelation) {
                ShowCancellationBar (view);
            } else {
                ShowRequestChoicesBar (view, calendarItem);
            }
        }

        void ShowRequestChoicesBar (View view, McCalendar calendarItem)
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

        void ShowAttendeeResponseBar (View view, McEmailMessage message)
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
            var bundle = downloader.Bundle;
            var downloaderWithWebView = (MessageDownloaderWithWebView)downloader;
            RenderBody (downloaderWithWebView.webView, bundle);
        }

        public void MessageDownloadDidFail (MessageDownloader downloader, NcResult result)
        {
            // TODO: show this inline, possibly with message preview (if available)
        }

        void RenderBody (Android.Webkit.WebView webView, NcEmailMessageBundle bundle)
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

        void DoneWithMessage ()
        {
        }

        void ArchiveButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ArchiveButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromSender (sender, out thread, out message);
            NcEmailArchiver.Archive (message);
            DoneWithMessage ();
        }

        void DeleteButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "DeleteButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromSender (sender, out thread, out message);
            NcEmailArchiver.Delete (message);
            DoneWithMessage ();
        }

        void ForwardButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ForwardButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromSender (sender, out thread, out message);
            StartComposeActivity (EmailHelper.Action.Forward, thread, message);
        }

        void ReplyButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ReplyButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromSender (sender, out thread, out message);
            StartComposeActivity (EmailHelper.Action.Reply, thread, message);
        }

        void ReplyAllButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ReplyAllButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromSender (sender, out thread, out message);
            StartComposeActivity (EmailHelper.Action.ReplyAll, thread, message);
        }

        void StartComposeActivity (EmailHelper.Action action, McEmailMessageThread thread, McEmailMessage message)
        {
            var activity = owner.Activity;
            owner.StartActivity (MessageComposeActivity.RespondIntent (activity, action, thread.FirstMessageId));
        }

        void DeadlinesView_Click ()
        {
            var folder = McFolder.GetDeadlineFakeFolder ();
            var intent = DeadlineActivity.ShowDeadlineFolderIntent (owner.Activity, folder);
            owner.StartActivity (intent);
        }

        void DeferredView_Click ()
        {
            var folder = McFolder.GetDeferredFakeFolder ();
            var intent = DeferredActivity.ShowDeferredFolderIntent (owner.Activity, folder);
            owner.StartActivity (intent); 
        }

        void InboxView_Click ()
        {
            var intent = new Intent ();
            intent.SetClass (owner.Activity, typeof(InboxActivity));
            intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
            owner.StartActivity (intent);
        }

        void BindSummaryViewHolder (RecyclerView.ViewHolder holder)
        {
            var summaryViewHolder = (SummaryViewHolder)holder;
 
            NcTask.Run (() => {
                int unreadMessageCount;
                int likelyMessageCount;
                int deferredMessageCount;
                int deadlineMessageCount;
                EmailHelper.GetMessageCounts (NcApplication.Instance.Account, out unreadMessageCount, out deferredMessageCount, out deadlineMessageCount, out likelyMessageCount);
                InvokeOnUIThread.Instance.Invoke (() => {
                    summaryViewHolder.inboxMessageCountView.Text = String.Format ("Go to Inbox ({0:N0} unread)", unreadMessageCount);
                    summaryViewHolder.deferredMessageCountView.Text = String.Format ("Go to Deferred Messages ({0:N0})", deferredMessageCount);
                    summaryViewHolder.deadlinesMessageCountView.Text = String.Format ("Go to Deadlines ({0:N0})", deadlineMessageCount);
                    // FIMXE LTR.
                });
            }, "UpdateUnreadMessageView");
        }

    }

    
}

