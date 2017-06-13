
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Views;
using Android.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Widget;
using Android.Support.V7.Widget;
using Android.Text;
using Android.Text.Style;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoClient.AndroidClient
{

    public class MessageListFragment : Fragment, MessageListAdapter.Listener, MessagesSyncManagerDelegate, NcContextMenuFragment
    {

        const int REQUEST_MOVE = 1;

        NachoEmailMessages Messages;
        MessageListAdapter MessagesAdapter;
        MessagesSyncManager SyncManager;

        #region Subviews

        SwipeRefreshLayout SwipeRefresh;
        MessageListFilterbar FilterBar;
        RecyclerView ListView;

        void FindSubviews (View view)
        {
            SwipeRefresh = view.FindViewById (Resource.Id.swipe_refresh_layout) as SwipeRefreshLayout;
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (view.Context));
            FilterBar = view.FindViewById (Resource.Id.filterbar) as MessageListFilterbar;
        }

        void ClearSubviews ()
        {
            SwipeRefresh = null;
            ListView = null;
            FilterBar.Cleanup ();
            FilterBar = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Android.OS.Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.MessageListFragment, container, false);
            FindSubviews (view);
            MessagesAdapter = new MessageListAdapter (this);
            MessagesAdapter.SetMessages (Messages);
            ListView.SetAdapter (MessagesAdapter);
            SwipeRefresh.Refresh += SwipeRefreshActivated;
            SyncManager = new MessagesSyncManager ();
            SyncManager.Delegate = this;
            UpdateFilterbar ();
            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            if (SyncManager.IsSyncing) {
                SyncManager.ResumeEvents ();
            }
            Messages.RefetchSyncTime ();
            Reload ();
            StartListeningForStatusInd ();
        }

        public override void OnPause ()
        {
            StopListeningForStatusInd ();
            SyncManager.PauseEvents ();
            base.OnPause ();
        }

        public override void OnDestroyView ()
        {
            SyncManager.Delegate = null;
            ClearSubviews ();
            base.OnDestroyView ();
        }

        public override void OnActivityResult (int requestCode, int resultCode, Intent data)
        {
            if (requestCode == REQUEST_MOVE) {
                if (resultCode == (int)Android.App.Result.Ok) {
                    var folderId = data.Extras.GetInt (FoldersActivity.EXTRA_FOLDER_ID);
                    MoveThread (SelectedThread, folderId);
                }
                SelectedThread = null;
            } else {
                base.OnActivityResult (requestCode, resultCode, data);
            }
        }

        #endregion

        #region Managing & Reloading Messages

        object MessagesLock = new object ();
        bool NeedsReload;
        bool IsReloading;
        protected bool HasLoadedOnce;

        public void SetEmailMessages (NachoEmailMessages messages)
        {
            lock (MessagesLock) {
                Messages = messages;
                if (MessagesAdapter != null) {
                    MessagesAdapter.SetMessages (Messages);
                }
            }
        }

        public void SetNeedsReload ()
        {
            NeedsReload = true;
            if (!IsReloading) {
                Reload ();
            }
        }

        void Reload ()
        {
            if (!IsReloading && !IsContextMenuOpen) {
                IsReloading = true;
                NeedsReload = false;
                if (Messages.HasBackgroundRefresh ()) {
                    Messages.BackgroundRefresh (HandleReloadResults);
                } else {
                    NcTask.Run (() => {
                        List<int> adds;
                        List<int> deletes;
                        NachoEmailMessages messages;
                        lock (MessagesLock) {
                            messages = Messages;
                        }
                        bool changed = messages.BeginRefresh (out adds, out deletes);
                        InvokeOnUIThread.Instance.Invoke (() => {
                            bool handledResults = false;
                            lock (MessagesLock) {
                                if (messages == Messages) {
                                    Messages.CommitRefresh ();
                                    HandleReloadResults (changed, adds, deletes);
                                    handledResults = true;
                                }
                            }
                            if (!handledResults) {
                                IsReloading = false;
                                if (NeedsReload) {
                                    Reload ();
                                }
                            }
                        });
                    }, "MessageListFragment.Reload");
                }
            }
        }

        void HandleReloadResults (bool changed, List<int> adds, List<int> deletes)
        {
            if (SwipeRefresh.Refreshing && !SyncManager.IsSyncing) {
                EndRefreshing ();
            }
            Messages.ClearCache ();
            if (!HasLoadedOnce) {
                HasLoadedOnce = true;
                MessagesAdapter.NotifyDataSetChanged ();
            } else {
                // FIXME: selective adds and deletes
                MessagesAdapter.NotifyDataSetChanged ();
            }
            IsReloading = false;
            if (NeedsReload) {
                Reload ();
            }
        }

        protected void ReloadTable ()
        {
            MessagesAdapter.NotifyDataSetChanged ();
        }

        void UpdateVisibleRows ()
        {
            // FIXME: could we do this without a full reload, like how we loop through visible items on iOS?
            MessagesAdapter.NotifyDataSetChanged ();
        }

        void SwipeRefreshActivated (object sender, EventArgs e)
        {
            StartSync ();
        }

        public void MessagesSyncDidComplete (MessagesSyncManager manager)
        {
            EndRefreshing ();
            ShowLastUpdatedToast ();
        }

        public void MessagesSyncDidTimeOut (MessagesSyncManager manager)
        {
            EndRefreshing ();
        }

        void EndRefreshing ()
        {
            SwipeRefresh.Refreshing = false;
        }

        void ShowLastUpdatedToast ()
        {
            DateTime? lastSyncDate = null;
            if (Messages != null) {
                lastSyncDate = Messages.LastSuccessfulSyncTime ();
            }
            if (lastSyncDate.HasValue) {
                var diff = DateTime.UtcNow - lastSyncDate.Value;
                string message;
                if (diff.TotalSeconds < 60) {
                    message = GetString (Resource.String.messages_sync_time_latest);
                } else {
                    var format = GetString (Resource.String.messages_sync_time_format);
                    message = String.Format (format, Pretty.TimeWithDecreasingPrecision (lastSyncDate.Value));
                }
                var toast = Toast.MakeText (Activity, message, ToastLength.Short);
                toast.Show ();
            }
        }

        void StartSync ()
        {
            if (!SyncManager.SyncEmailMessages (Messages)) {
                MessagesAdapter.NotifyDataSetChanged ();
                EndRefreshing ();
            }
        }

        protected void CancelSyncing ()
        {
            SyncManager.Cancel ();
            EndRefreshing ();
        }

        #endregion

        #region Filtering

        protected bool ShouldShowFilterBar {
        	get {
        		return Messages.HasFilterSemantics () && Messages.PossibleFilterSettings.Length > 1;
            }
        }

        protected void UpdateFilterbar ()
        {
            if (ShouldShowFilterBar) {
                FilterBar.Visibility = ViewStates.Visible;

                var items = new List<MessageListFilterbar.Item> ();
                var filters = Messages.PossibleFilterSettingsMask;
                MessageListFilterbar.Item selectedItem = null;

                if (filters.HasFlag (FolderFilterOptions.All)) {
                    items.Add (new MessageListFilterbar.Item (Resource.String.messages_filter_all, Resource.Drawable.email_filter_all, Resource.Drawable.email_filter_all_selected, FilterAll));
                    if (Messages.FilterSetting == FolderFilterOptions.All) {
                        selectedItem = items.Last ();
                    }
                }
                if (filters.HasFlag (FolderFilterOptions.Hot)) {
                    items.Add (new MessageListFilterbar.Item (Resource.String.messages_filter_hot, Resource.Drawable.email_filter_hot, Resource.Drawable.email_filter_hot_selected, FilterHot));
                    if (Messages.FilterSetting == FolderFilterOptions.Hot) {
                        selectedItem = items.Last ();
                    }
                }
                if (filters.HasFlag (FolderFilterOptions.Unread)) {
                    items.Add (new MessageListFilterbar.Item (Resource.String.messages_filter_unread, Resource.Drawable.email_filter_unread, Resource.Drawable.email_filter_unread_selected, FilterUnread));
                    if (Messages.FilterSetting == FolderFilterOptions.Unread) {
                        selectedItem = items.Last ();
                    }
                }
                if (filters.HasFlag (FolderFilterOptions.Focused)) {
                    items.Add (new MessageListFilterbar.Item (Resource.String.messages_filter_focus, Resource.Drawable.email_filter_focus, Resource.Drawable.email_filter_focus_selected, FilterFocus));
                    if (Messages.FilterSetting == FolderFilterOptions.Focused) {
                        selectedItem = items.Last ();
                    }
                }

                FilterBar.SetItems (items.ToArray ());
                FilterBar.SelectItem (selectedItem);

            } else {
                FilterBar.Visibility = ViewStates.Gone;
            }
        }



        void FilterAll ()
        {
            //EndAllTableEdits ();
            Messages.FilterSetting = FolderFilterOptions.All;
            SetNeedsReload ();
        }

        void FilterHot ()
        {
            //EndAllTableEdits ();
            Messages.FilterSetting = FolderFilterOptions.Hot;
            SetNeedsReload ();
        }

        void FilterUnread ()
        {
            //EndAllTableEdits ();
            Messages.FilterSetting = FolderFilterOptions.Unread;
            SetNeedsReload ();
        }

        void FilterFocus ()
        {
        	//EndAllTableEdits ();
        	Messages.FilterSetting = FolderFilterOptions.Focused;
        	SetNeedsReload ();
        }

        #endregion

        #region Context Menus

        bool IsContextMenuOpen = false;

        public void OnContextMenuClosed (IMenu menu)
        {
            IsContextMenuOpen = false;
            if (NeedsReload) {
                Reload ();
            }
        }

        public override bool OnContextItemSelected (IMenuItem item)
        {
            var position = -1;
            if (item.Intent != null && item.Intent.HasExtra (MessageListAdapter.EXTRA_POSITION)) {
                position = item.Intent.Extras.GetInt (MessageListAdapter.EXTRA_POSITION);
            }
            if (position >= 0) {
                switch (item.ItemId) {
                case Resource.Id.forward:
                    ForwardMessageAtPosition (position);
                    return true;
                case Resource.Id.move:
                    MoveMessageAtPosition (position);
                    return true;
                case Resource.Id.create_event:
                    CreateEventFromMessageAtPosition (position);
                    return true;
                case Resource.Id.archive:
                    ArchiveMessageAtPosition (position);
                    return true;
                case Resource.Id.delete:
                    DeleteMessageAtPosition (position);
                    return true;
                case Resource.Id.reply:
                    ReplyToMessageAtPosition (position);
                    return true;
                case Resource.Id.read:
                    MarkAsReadThreadAtPosition (position);
                    return true;
                case Resource.Id.unread:
                    MarkAsUnreadThreadAtPosition (position);
                    return true;
                }
            }
            return base.OnContextItemSelected (item);
        }

        void ReplyToMessageAtPosition (int position)
        {
            var message = Messages.GetCachedMessage (position);
            if (message != null) {
                ComposeReply (message);
            }
        }

        void ForwardMessageAtPosition (int position)
        {
            var message = Messages.GetCachedMessage (position);
            if (message != null) {
                ComposeForward (message);
            }
        }

        void MoveMessageAtPosition (int position)
        {
            var thread = Messages.GetEmailThread (position);
            var message = Messages.GetCachedMessage (position);
            if (thread != null && message != null) {
                ShowMoveOptions (thread, message.AccountId);
            }
        }

        void DeleteMessageAtPosition (int position)
        {
            var thread = Messages.GetEmailThread (position);
            if (thread != null) {
                DeleteThread (thread);
                RemoveItemViewAtPosition (position);
            }
        }

        void ArchiveMessageAtPosition (int position)
        {
            var thread = Messages.GetEmailThread (position);
            if (thread != null) {
                ArchiveThread (thread);
                RemoveItemViewAtPosition (position);
            }
        }

        void CreateEventFromMessageAtPosition (int position)
        {
            var message = Messages.GetCachedMessage (position);
            if (message != null) {
                CreateEvent (message);
            }
        }

        void MarkAsReadThreadAtPosition (int position)
        {
            var thread = Messages.GetEmailThread (position);
            var message = Messages.GetCachedMessage (position);
            if (thread != null && message != null) {
                MarkAsRead (thread);
                message.IsRead = true;
                MessagesAdapter.NotifyItemChanged (position);
            }
        }

        void MarkAsUnreadThreadAtPosition (int position)
        {
            var thread = Messages.GetEmailThread (position);
            var message = Messages.GetCachedMessage (position);
            if (thread != null && message != null) {
                MarkAsUnread (thread);
                message.IsRead = false;
                MessagesAdapter.NotifyItemChanged (position);
            }
        }

        #endregion

        #region System Events

        bool IsListeningForStatusInd = false;

        void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent += StatusIndCallback;
                IsListeningForStatusInd = true;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent -= StatusIndCallback;
                IsListeningForStatusInd = false;
            }
        }

        void StatusIndCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (s.Account == null || (Messages != null && Messages.IsCompatibleWithAccount (s.Account))) {
                switch (s.Status.SubKind) {
                case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
					SetNeedsReload ();
                    break;
                case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
                case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
                case NcResult.SubKindEnum.Info_EmailMessageScoreUpdated:
                case NcResult.SubKindEnum.Info_EmailMessageChanged:
                case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
					UpdateVisibleRows ();
                    break;
                case NcResult.SubKindEnum.Error_SyncFailed:
                case NcResult.SubKindEnum.Info_SyncSucceeded:
                    Messages.RefetchSyncTime ();
                    break;
                }
            }
        }

        #endregion

        #region Adapter Listener

        public void OnContextMenuCreated ()
        {
            IsContextMenuOpen = true;
        }

        public void OnMessageSelected (McEmailMessage message, McEmailMessageThread thread)
        {
            if (Messages.HasDraftsSemantics ()) {
                ComposeDraft (message);
            } else if (Messages.HasOutboxSemantics ()) {
                ShowOutboxMessage (message);
            } else if (thread.HasMultipleMessages ()) {
                ShowThread (thread);
            } else {
                ShowMessage (message);
            }
        }

        #endregion

        #region Private Helpers

        void ComposeDraft (McEmailMessage message)
        {
            var intent = MessageComposeActivity.DraftIntent (Activity, message);
			StartActivity (intent);
        }

        void ComposeOutboxMessage (McEmailMessage message)
        {
        	var copy = EmailHelper.MoveFromOutboxToDrafts (message);
        	ComposeDraft (copy);
        }

        void ComposeForward (McEmailMessage message)
        {
            var intent = MessageComposeActivity.RespondIntent (Activity, EmailHelper.Action.Forward, message);
            StartActivity (intent);
        }

        void CreateEvent (McEmailMessage message)
        {
            // TODO: launch event activity
        }

        void MarkAsRead (McEmailMessageThread thread)
        {
            EmailHelper.MarkAsRead (thread, force: true);
        }

        void MarkAsUnread (McEmailMessageThread thread)
        {
            EmailHelper.MarkAsUnread (thread, force: true);
        }

        void ShowOutboxMessage (McEmailMessage message)
        {
            var pending = McPending.QueryByEmailMessageId (message.AccountId, message.Id);
            if (pending != null && pending.ResultKind == NcResult.KindEnum.Error) {
				ShowOutboxError (message, pending);
            } else {
				ComposeOutboxMessage (message);
            }
        }

        void ShowThread (McEmailMessageThread thread)
        {
            var folder = Messages.GetFolderForThread (thread);
            var intent = MessageListActivity.BuildThreadIntent (Activity, folder, thread);
            StartActivity (intent);
        }

        void ShowMessage (McEmailMessage message)
        {
            var intent = MessageViewActivity.BuildIntent (Activity, message.Id);
            StartActivity (intent);
        }

        McEmailMessageThread SelectedThread;

        void ShowMoveOptions (McEmailMessageThread thread, int accountId)
        {
            SelectedThread = thread;
            var intent = FoldersActivity.BuildIntent (Activity, accountId);
            StartActivityForResult (intent, REQUEST_MOVE);
        }

        void MoveThread (McEmailMessageThread thread, int folderId)
        {
            NcTask.Run (() => {
                var folder = McFolder.QueryById<McFolder> (folderId);
                NcEmailArchiver.Move (thread, folder);
            }, "MessageListFragment_Move");
            Messages.IgnoreMessage (thread.FirstMessageId);
        }

        void DeleteThread (McEmailMessageThread thread)
        {
            if (Messages.HasOutboxSemantics ()) {
                NcTask.Run (() => {
                    EmailHelper.DeleteEmailThreadFromOutbox (thread);
                }, "MessageListFragment_Delete");
            } else if (Messages.HasDraftsSemantics ()) {
                NcTask.Run (() => {
                    EmailHelper.DeleteEmailThreadFromDrafts (thread);
                }, "MessageListFragment_Delete");
            } else {
                NcTask.Run (() => {
                    NcEmailArchiver.Delete (thread);
                }, "MessageListFragment_Delete");
            }
            Messages.IgnoreMessage (thread.FirstMessageId);
        }

        void ArchiveThread (McEmailMessageThread thread)
        {
    		NcTask.Run (() => {
                NcEmailArchiver.Archive (thread);
    		}, "MessageListFragment_Archive");
        	Messages.IgnoreMessage (thread.FirstMessageId);
        }

        void ComposeReply (McEmailMessage message)
        {
            var intent = MessageComposeActivity.RespondIntent (Activity, EmailHelper.Action.ReplyAll, message);
			StartActivity (intent);
        }

        void RemoveItemViewAtPosition (int position)
        {
            if (!IsReloading) {
                Messages.RemoveIgnoredMessages ();
                MessagesAdapter.NotifyItemRemoved (position);
            } else {
                SetNeedsReload ();
            }
        }

        void ShowOutboxError (McEmailMessage message, McPending pending)
        {
        	string errorString;
        	if (!ErrorHelper.ErrorStringForSubkind (pending.ResultSubKind, out errorString)) {
        		errorString = String.Format ("(ErrorCode={0}", pending.ResultSubKind);
        	}
        	var messageString = "There was a problem sending this message.  You can resend this message or open it in the drafts folder.";
        	var alertString = String.Format ("{0}\n{1}", messageString, errorString);
            var builder = new Android.App.AlertDialog.Builder (Activity);
            builder.SetMessage (alertString);
            builder.SetPositiveButton ("Edit Message", (dialog, which) => {
                ComposeOutboxMessage (message);
            });
            builder.SetNegativeButton ("Cancel", (dialog, which) => {
            });
            var alert = builder.Create ();
            alert.Show ();
        }

        #endregion

    }

    public class MessageListAdapter : RecyclerView.Adapter
    {

        public const string EXTRA_POSITION = "NachoClient.AndroidClient.MessageListAdapter.EXTRA_POSITION";

        public interface Listener
        {
            void OnMessageSelected (McEmailMessage message, McEmailMessageThread thread);
            void OnContextMenuCreated ();
        }

        NachoEmailMessages Messages;
        WeakReference<Listener> WeakListener;

        public MessageListAdapter (Listener listener) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
        }

        public void SetMessages (NachoEmailMessages messages)
        {
            Messages = messages;
        }

        public override int ItemCount {
            get {
                return Messages.Count ();
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            var holder = MessageViewHolder.Create (parent);
            holder.ContentView.Click += (sender, e) => {
                ItemClicked (holder.AdapterPosition);
            };
            holder.ContentView.ContextMenuCreated += (sender, e) => {
               ItemContextMenuCreated (holder.AdapterPosition, e.Menu);
            };
            return holder;
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            var messageHolder = (holder as MessageViewHolder);
            var message = Messages.GetCachedMessage (position);
            var thread = Messages.GetEmailThread (position);
            messageHolder.UseRecipientName = Messages.HasOutboxSemantics () || Messages.HasDraftsSemantics () || Messages.HasSentSemantics ();
            messageHolder.SetMessage (message, thread.MessageCount);
            if (Messages.IncludesMultipleAccounts ()) {
                messageHolder.IndicatorColor = Util.ColorForAccount (message.AccountId);
            } else {
                messageHolder.IndicatorColor = 0;
            }
            if (Messages.HasOutboxSemantics ()) {
                var pending = McPending.QueryByEmailMessageId (message.AccountId, message.Id);
                if (pending != null && pending.ResultKind == NcResult.KindEnum.Error) {
                    // TODO: indicate error
                } else {
                    // TODO: hide error
                }
            } else {
                // TODO: hide error
            }
            if (message.BodyId == 0) {
                NcTask.Run (() => {
                    BackEnd.Instance.SendEmailBodyFetchHint (message.AccountId, message.Id);
                }, "MessageListFragment.SendEmailBodyFetchHint");
            }
        }

        void ItemClicked (int position)
        {
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                var message = Messages.GetCachedMessage (position);
                var thread = Messages.GetEmailThread (position);
                if (message != null && thread != null) {
                    listener.OnMessageSelected (message, thread);
                }
            }
        }

        void ItemContextMenuCreated (int position, IContextMenu menu)
        {
            var message = Messages.GetCachedMessage (position);
            var intent = new Intent ();
            intent.PutExtra (EXTRA_POSITION, position);
            int order = 0;
            List<IMenuItem> items = new List<IMenuItem> ();
            items.Add (menu.Add (0, Resource.Id.reply, order++, Resource.String.message_item_action_reply));
            items.Add (menu.Add (0, Resource.Id.forward, order++, Resource.String.message_item_action_forward));
            items.Add (menu.Add (0, Resource.Id.move, order++, Resource.String.message_item_action_move));
            items.Add (menu.Add (0, Resource.Id.archive, order++, Resource.String.message_item_action_archive));
            items.Add (menu.Add (0, Resource.Id.delete, order++, Resource.String.message_item_action_delete));
            if (message.IsRead) {
                items.Add (menu.Add (0, Resource.Id.unread, order++, Resource.String.message_item_action_unread));
            } else {
                items.Add (menu.Add (0, Resource.Id.read, order++, Resource.String.message_item_action_read));
            }
            foreach (var item in items) {
                item.SetIntent (intent);
            }
            menu.SetHeaderTitle (message.Subject);
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                listener.OnContextMenuCreated ();
            }
        }
    }

    public class MessageViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
    {

        public bool UseRecipientName = false;
        private int _IndicatorColor = 0;
        public int IndicatorColor {
            get {
                return _IndicatorColor;
            }
            set {
                _IndicatorColor = value;
                if (_IndicatorColor == 0) {
                    AccountIndicatorView.Visibility = ViewStates.Gone;
                } else {
                    AccountIndicatorView.Visibility = ViewStates.Visible;
                    AccountIndicatorView.SetBackgroundColor (new Android.Graphics.Color (_IndicatorColor));
                }
            }
        }

        public override View ClickTargetView {
            get {
                return ContentView;
            }
        }

        public View ContentView { get; private set; }
        public View BackgroundView { get; private set; }
        View AccountIndicatorView;
        TextView MainLabel;
        TextView DetailLabel;
        TextView DateLabel;
        View PortraitFrame;
        PortraitView PortraitView;
        ImageView UnreadIndicator;
        View ThreadIndicator;
        TextView ThreadCountLabel;

        public static MessageViewHolder Create (ViewGroup parent)
        {
            var view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageListItem, parent, false);
            return new MessageViewHolder (view);
        }

        public MessageViewHolder (View view) : base (view)
        {
            FindSubviews ();
        }

        void FindSubviews ()
        {
            ContentView = ItemView.FindViewById (Resource.Id.content);
            BackgroundView = ItemView.FindViewById (Resource.Id.background);
            AccountIndicatorView = ItemView.FindViewById (Resource.Id.account_indicator);
            MainLabel = ItemView.FindViewById (Resource.Id.main_label) as TextView;
            DetailLabel = ItemView.FindViewById (Resource.Id.detail_label) as TextView;
            DateLabel = ItemView.FindViewById (Resource.Id.date_label) as TextView;
            PortraitFrame = ItemView.FindViewById (Resource.Id.portrait_frame);
            PortraitView = ItemView.FindViewById (Resource.Id.portrait_view) as PortraitView;
            UnreadIndicator = ItemView.FindViewById (Resource.Id.unread_indicator) as ImageView;
            ThreadIndicator = ItemView.FindViewById (Resource.Id.thread_indicator);
            ThreadCountLabel = ItemView.FindViewById (Resource.Id.thread_count) as TextView;
        }

        public void SetMessage (McEmailMessage message, int threadCount)
        {
            if (UseRecipientName) {
                MainLabel.Text = Pretty.RecipientString (message.To);
                PortraitFrame.Visibility = ViewStates.Gone;
            } else {
                MainLabel.Text = Pretty.SenderString (message.From);
                PortraitView.SetPortrait (message.cachedPortraitId, message.cachedFromColor, message.cachedFromLetters);
                PortraitFrame.Visibility = ViewStates.Visible;
            }
            int subjectLength;
            var previewText = Pretty.MessagePreview (message, out subjectLength);
            int attachmentIndex = -1;
            if (message.cachedHasAttachments) {
                if (subjectLength > 0) {
                    previewText = previewText.Substring (0, subjectLength) + "  " + previewText.Substring (subjectLength);
                    attachmentIndex = subjectLength + 1;
                } else {
                    previewText = "  " + previewText;
                    attachmentIndex = 0;
                }
            }
            var styledPreview = new SpannableString (previewText);
            styledPreview.SetSpan (new ForegroundColorSpan (ThemeColor (Android.Resource.Attribute.ColorPrimary)), 0, subjectLength, 0);
            // TODO: insert hot icon
            if (attachmentIndex >= 0) {
                var imageSpan = new ImageSpan (ItemView.Context, Resource.Drawable.subject_attach);
                styledPreview.SetSpan (imageSpan, attachmentIndex, attachmentIndex + 1, 0);
            }
            DetailLabel.SetText (styledPreview, TextView.BufferType.Spannable);
            // TODO: intents as part of date ("due by" prefix)
            DateLabel.Text = Pretty.TimeWithDecreasingPrecision (message.DateReceived);
            if (threadCount > 1) {
                ThreadIndicator.Visibility = ViewStates.Visible;
                ThreadCountLabel.Text = String.Format ("{0}", threadCount);
            } else {
                ThreadIndicator.Visibility = ViewStates.Gone;
            }
            UnreadIndicator.Visibility = message.IsRead ? ViewStates.Gone : ViewStates.Visible;
        }

        public Android.Graphics.Color ThemeColor (int attr)
        {
            var typedVal = new Android.Util.TypedValue ();
            ItemView.Context.Theme.ResolveAttribute (attr, typedVal, true);
            return (ItemView.Context.GetDrawable (typedVal.ResourceId) as Android.Graphics.Drawables.ColorDrawable).Color;
        }

    }
}
