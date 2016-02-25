//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using Android.Views;
using Android.Webkit;
using Android.Widget;
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
            return !searching && CARDVIEW_STYLE == currentStyle && ItemCount == position + 1;
        }

        public override int GetItemViewType (int position)
        {
            if (IsFooterPosition (position)) {
                return SUMMARY_STYLE;
            } else {
                return currentStyle;
            }
        }

        public class CellViewHolder : RecyclerView.ViewHolder
        {
            public Bind.MessageHeaderViewHolder mvh;
            public ImageView multiSelectView;
            public TextView previewView;

            public CellViewHolder (View itemView, Action<int> click, Action<int,ImageView> chiliClick) : base (itemView)
            {
                mvh = new Bind.MessageHeaderViewHolder (itemView);
                itemView.Click += (object sender, EventArgs e) => click (AdapterPosition);
                mvh.chiliView.Click += (object sender, EventArgs e) => chiliClick (AdapterPosition, mvh.chiliView);
                previewView = itemView.FindViewById<Android.Widget.TextView> (Resource.Id.preview);
                multiSelectView = itemView.FindViewById<Android.Widget.ImageView> (Resource.Id.selected);
            }
        }

        public class CardViewHolder : RecyclerView.ViewHolder
        {
            public Bind.MessageHeaderViewHolder mvh;
            public ViewGroup parent;
            public WebView webview;

            public CardViewHolder (View itemView, Action<int> click, Action<int,ImageView> chiliClick,
                                   Action<int> replyClick, Action<int> replyAllClick, Action<int> forwardClick, Action<int> archiveClick, Action<int> deleteClick) : base (itemView)
            {
                mvh = new Bind.MessageHeaderViewHolder (itemView);

                itemView.Click += (object sender, EventArgs e) => click (AdapterPosition);
                mvh.chiliView.Click += (object sender, EventArgs e) => chiliClick (AdapterPosition, mvh.chiliView);

                var replyButton = itemView.FindViewById (Resource.Id.reply);
                replyButton.Click += (object sender, EventArgs e) => replyClick (AdapterPosition);

                var replyAllButton = itemView.FindViewById (Resource.Id.reply_all);
                replyAllButton.Click += (object sender, EventArgs e) => replyAllClick (AdapterPosition);

                var forwardButton = itemView.FindViewById (Resource.Id.forward);
                forwardButton.Click += (object sender, EventArgs e) => forwardClick (AdapterPosition);

                var archiveButton = itemView.FindViewById (Resource.Id.archive);
                archiveButton.Click += (object sender, EventArgs e) => archiveClick (AdapterPosition);

                var deleteButton = itemView.FindViewById (Resource.Id.delete);
                deleteButton.Click += (object sender, EventArgs e) => deleteClick (AdapterPosition);

                webview = itemView.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
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
                return owner.CurrentMessages.GetEmailThread (position);
            }
        }

        public override long GetItemId (int position)
        {
            if (IsFooterPosition (position)) {
                return 0; // No item has 0 Id
            }
            return owner.CurrentMessages.GetEmailThread (position).FirstMessageId;
        }

        public override int ItemCount {
            get {
                int count = owner.CurrentMessages.Count ();
                if (!searching && CARDVIEW_STYLE == currentStyle) {
                    count += 1; // extra row for the footer
                }
                return count;
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            switch (viewType) {
            case LISTVIEW_STYLE:
                return CreateCellViewHolder (parent);
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

        RecyclerView.ViewHolder CreateCellViewHolder (ViewGroup parent)
        {
            var itemView = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageCell, parent, false);
            return new CellViewHolder (itemView, ItemView_Click, ChiliView_Click);
        }

        RecyclerView.ViewHolder CreateCardViewHolder (ViewGroup parent)
        {
            var itemView = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageCard, parent, false);

            var webView = itemView.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
            webView.Clickable = false;
            webView.LongClickable = false;
            webView.Focusable = false;
            webView.FocusableInTouchMode = false;
            webView.SetOnTouchListener (new IgnoreTouchListener ());

            itemView.SetMinimumHeight (parent.MeasuredHeight);
            itemView.LayoutParameters.Height = parent.MeasuredHeight;

            var vh = new CardViewHolder (itemView, ItemView_Click, ChiliView_Click, ReplyButton_Click, ReplyAllButton_Click, ForwardButton_Click, ArchiveButton_Click, DeleteButton_Click);
            vh.parent = parent;
            return vh;
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
                BindCellView ((CellViewHolder)holder, position);
                break;
            case CARDVIEW_STYLE:
                BindCardView ((CardViewHolder)holder, position);
                break;
            case SUMMARY_STYLE:
                BindSummaryViewHolder ((SummaryViewHolder)holder);
                break;
            default:
                NcAssert.CaseError ();
                break;
            }
        }

        void BindCellView (CellViewHolder vh, int position)
        {
            var thread = owner.CurrentMessages.GetEmailThread (position);
            McEmailMessage message;
            if (searching) {
                message = thread.FirstMessageSpecialCase ();
            } else {
                message = owner.GetCachedMessage (position);
            }
            var isDraft = owner.CurrentMessages.HasDraftsSemantics () || owner.CurrentMessages.HasOutboxSemantics ();
            Bind.BindMessageHeader (thread, message, vh.mvh, isDraft);

            // Preview label view                
            if (null == message) {
                vh.previewView.Text = "";
            } else {
                var cookedPreview = EmailHelper.AdjustPreviewText (message.GetBodyPreviewOrEmpty ());
                vh.previewView.SetText (Android.Text.Html.FromHtml (cookedPreview), Android.Widget.TextView.BufferType.Spannable);
            }

            if (owner.multiSelectActive) {
                vh.multiSelectView.Visibility = ViewStates.Visible;
                if (owner.MultiSelectSet.Contains (position)) {
                    vh.multiSelectView.SetImageResource (Resource.Drawable.gen_checkbox_checked);
                } else {
                    vh.multiSelectView.SetImageResource (Resource.Drawable.gen_checkbox);
                }
            } else {
                vh.multiSelectView.Visibility = ViewStates.Invisible;
            }
        }

        void MessageFromPosition (int position, out McEmailMessageThread thread, out McEmailMessage message)
        {
            thread = owner.CurrentMessages.GetEmailThread (position);
            message = thread.FirstMessageSpecialCase ();
        }

        void ItemView_Click (int position)
        {
            if (null != onMessageClick) {
                onMessageClick (this, position);
            }
        }

        void ChiliView_Click (int position, ImageView chiliView)
        {
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromPosition (position, out thread, out message);
            if (null != message) {
                NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
                Bind.BindMessageChili (thread, message, chiliView);
            }
        }

        class MessageDownloaderWithWebView : MessageDownloader
        {
            public WebView webView;
        }

        public class IgnoreTouchListener : Java.Lang.Object, View.IOnTouchListener
        {
            public bool OnTouch (View v, MotionEvent e)
            {
                return false;
            }
        }

        void BindCardView (CardViewHolder vh, int position)
        {
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromPosition (position, out thread, out message);

            Bind.BindMessageHeader (thread, message, vh.mvh);
            vh.mvh.subjectView.SetMaxLines (100);

            BindMeetingRequest (vh.ItemView, message);

            if (null != message) {
                NcEmailMessageBundle bundle;
                if (message.BodyId != 0) {
                    bundle = new NcEmailMessageBundle (message);
                } else {
                    bundle = null;
                }
                if (bundle == null || bundle.NeedsUpdate) {
                    var messageDownloader = new MessageDownloaderWithWebView ();
                    messageDownloader.webView = vh.webview;
                    messageDownloader.Bundle = bundle;
                    messageDownloader.Delegate = this;
                    messageDownloader.Download (message);
                } else {
                    RenderBody (vh.webview, bundle);
                }
            }
            vh.ItemView.SetMinimumHeight (vh.parent.MeasuredHeight);
            vh.ItemView.LayoutParameters.Height = vh.parent.MeasuredHeight;
        }

        void BindMeetingRequest (View view, McEmailMessage message)
        {
            if ((null == message) || (null == message.MeetingRequest)) {
                view.FindViewById<View> (Resource.Id.event_in_message).Visibility = ViewStates.Gone;
                return;
            }

            var meeting = message.MeetingRequest;
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
                var color = Util.ColorResourceForEmail (message.AccountId, email);
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
                        var color = Util.ColorResourceForEmail (message.AccountId, attendee.Address);
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

        void ArchiveButton_Click (int position)
        {
            Log.Info (Log.LOG_UI, "ArchiveButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromPosition (position, out thread, out message);
            if (null != message) {
                NcEmailArchiver.Archive (message);
            }
            DoneWithMessage ();
        }

        void DeleteButton_Click (int position)
        {
            Log.Info (Log.LOG_UI, "DeleteButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromPosition (position, out thread, out message);
            if (null != message) {
                NcEmailArchiver.Delete (message);
            }
            DoneWithMessage ();
        }

        void ForwardButton_Click (int position)
        {
            Log.Info (Log.LOG_UI, "ForwardButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromPosition (position, out thread, out message);
            if (null != message) {
                StartComposeActivity (EmailHelper.Action.Forward, thread, message);
            }
        }

        void ReplyButton_Click (int position)
        {
            Log.Info (Log.LOG_UI, "ReplyButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromPosition (position, out thread, out message);
            if (null != message) {
                StartComposeActivity (EmailHelper.Action.Reply, thread, message);
            }
        }

        void ReplyAllButton_Click (int position)
        {
            Log.Info (Log.LOG_UI, "ReplyAllButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromPosition (position, out thread, out message);
            if (null != message) {
                StartComposeActivity (EmailHelper.Action.ReplyAll, thread, message);
            }
        }

        void StartComposeActivity (EmailHelper.Action action, McEmailMessageThread thread, McEmailMessage message)
        {
            var activity = owner.Activity;
            owner.StartActivity (MessageComposeActivity.RespondIntent (activity, action, thread.FirstMessage()));
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

        void BindSummaryViewHolder (SummaryViewHolder summaryViewHolder)
        {
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
