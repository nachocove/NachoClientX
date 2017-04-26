
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

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoClient.AndroidClient
{

    public class MessageListFragment : Fragment, MessageListAdapter.Listener, MessagesSyncManagerDelegate
    {

        NachoEmailMessages Messages;
        MessageListAdapter MessagesAdapter;
        MessagesSyncManager SyncManager;

        #region Subviews

        SwipeRefreshLayout SwipeRefresh;
        RecyclerView ListView;

        void FindSubviews (View view)
        {
            SwipeRefresh = view.FindViewById (Resource.Id.swipe_refresh_layout) as SwipeRefreshLayout;
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (view.Context));
        }

        void ClearSubviews ()
        {
            SwipeRefresh = null;
            ListView = null;
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
            if (!IsReloading) {
                IsReloading = true;
                NeedsReload = false;
                if (Messages.HasBackgroundRefresh ()) {
                    Messages.BackgroundRefresh (HandleReloadResults);
                } else {
                    NcTask.Run (() => {
                        List<int> adds;
                        List<int> deletes;
                        NachoEmailMessages messages;
                        lock (MessagesLock){
                            messages = Messages;
                        }
                        bool changed = messages.BeginRefresh (out adds, out deletes);
                        InvokeOnUIThread.Instance.Invoke (() => {
                            bool handledResults = false;
                            lock (MessagesLock){
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
        }

        void ShowOutboxMessage (McEmailMessage message)
        {
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

        #endregion

    }

    public class MessageListAdapter : RecyclerView.Adapter
    {

        public interface Listener
        {
            void OnMessageSelected (McEmailMessage message, McEmailMessageThread thread);
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
            holder.ItemView.Click += (sender, e) => {
                ItemClicked (holder.AdapterPosition);
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
    }

    public class MessageViewHolder : RecyclerView.ViewHolder
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
            var styledPreview = new SpannableString (previewText);
            styledPreview.SetSpan (new ForegroundColorSpan (ThemeColor (Android.Resource.Attribute.ColorPrimary)), 0, subjectLength, 0);
            // TODO: insert hot icon
            // TODO: insert attachment icon
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
