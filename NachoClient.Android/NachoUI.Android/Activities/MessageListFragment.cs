
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;

//using Android.Util;
using Android.Views;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using Android.Graphics.Drawables;
using NachoCore.Brain;

namespace NachoClient.AndroidClient
{
    public class MessageListFragment : Fragment
    {
        private const int ARCHIVE_TAG = 1;
        private const int SAVE_TAG = 2;
        private const int DELETE_TAG = 3;
        private const int DEFER_TAG = 4;

        SwipeMenuListView listView;
        MessageListAdapter messageListAdapter;

        SwipeRefreshLayout mSwipeRefreshLayout;

        INachoEmailMessages messages;

        Android.Widget.ImageView composeButton;

        public event EventHandler<McEmailMessageThread> onMessageClick;

        public static MessageListFragment newInstance (INachoEmailMessages messages)
        {
            var fragment = new MessageListFragment ();
            fragment.messages = messages;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.MessageListFragment, container, false);

            var activity = (NcActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            mSwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            mSwipeRefreshLayout.SetColorSchemeResources (Resource.Color.refresh_1, Resource.Color.refresh_2, Resource.Color.refresh_3);

            mSwipeRefreshLayout.Refresh += (object sender, EventArgs e) => {
                var nr = messages.StartSync ();
                rearmRefreshTimer (NachoSyncResult.DoesNotSync (nr) ? 3 : 10);
            };

            composeButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            composeButton.SetImageResource (Resource.Drawable.contact_newemail);
            composeButton.Visibility = Android.Views.ViewStates.Visible;
            composeButton.Click += ComposeButton_Click;

            // Highlight the tab bar icon of this activity
            var inboxImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.inbox_image);
            inboxImage.SetImageResource (Resource.Drawable.nav_mail_active);

            messageListAdapter = new MessageListAdapter (messages);

            listView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.Adapter = messageListAdapter;

            listView.ItemClick += ListView_ItemClick;

            listView.setMenuCreator ((menu) => {
                SwipeMenuItem deferItem = new SwipeMenuItem (Activity.ApplicationContext);
                deferItem.setBackground (new ColorDrawable (A.Color_NachoSwipeEmailDefer));
                deferItem.setWidth (dp2px (90));
                deferItem.setTitle ("Defer");
                deferItem.setTitleSize (14);
                deferItem.setTitleColor (A.Color_White);
                deferItem.setIcon (A.Id_NachoSwipeEmailDefer);
                deferItem.setId (DEFER_TAG);
                menu.addMenuItem (deferItem, SwipeMenu.SwipeSide.LEFT);
                SwipeMenuItem moveItem = new SwipeMenuItem (Activity.ApplicationContext);
                moveItem.setBackground (new ColorDrawable (A.Color_NachoSwipeEmailMove));
                moveItem.setWidth (dp2px (90));
                moveItem.setTitle ("Move");
                moveItem.setTitleSize (14);
                moveItem.setTitleColor (A.Color_White);
                moveItem.setIcon (A.Id_NachoSwipeEmailMove);
                moveItem.setId (SAVE_TAG);
                menu.addMenuItem (moveItem, SwipeMenu.SwipeSide.LEFT);
                SwipeMenuItem archiveItem = new SwipeMenuItem (Activity.ApplicationContext);
                archiveItem.setBackground (new ColorDrawable (A.Color_NachoSwipeEmailArchive));
                archiveItem.setWidth (dp2px (90));
                archiveItem.setTitle ("Archive");
                archiveItem.setTitleSize (14);
                archiveItem.setTitleColor (A.Color_White);
                archiveItem.setIcon (A.Id_NachoSwipeEmailArchive);
                archiveItem.setId (ARCHIVE_TAG);
                menu.addMenuItem (archiveItem, SwipeMenu.SwipeSide.RIGHT);
                SwipeMenuItem deleteItem = new SwipeMenuItem (Activity.ApplicationContext);
                deleteItem.setBackground (new ColorDrawable (A.Color_NachoSwipeEmailDelete));
                deleteItem.setWidth (dp2px (90));
                deleteItem.setTitle ("Delete");
                deleteItem.setTitleSize (14);
                deleteItem.setTitleColor (A.Color_White);
                deleteItem.setIcon (A.Id_NachoSwipeEmailDelete);
                deleteItem.setId (DELETE_TAG);
                menu.addMenuItem (deleteItem, SwipeMenu.SwipeSide.RIGHT);
            });

            listView.setOnMenuItemClickListener (( position, menu, index) => {
                var messageThread = messages.GetEmailThread (position);
                switch (index) {
                case SAVE_TAG:
                    ShowFolderChooser (messageThread);
                    break;
                case DEFER_TAG:
                    ShowPriorityChooser (messageThread);
                    break;
                case ARCHIVE_TAG:
                    ArchiveThisMessage (messageThread);
                    break;
                case DELETE_TAG:
                    DeleteThisMessage (messageThread);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action index {0}", index));
                }
                return false;
            });

            return view;
        }

        void ListView_ItemClick (object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            if (null != onMessageClick) {
                onMessageClick (this, messageListAdapter [e.Position]);
            }
        }

        void ComposeButton_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(MessageComposeActivity));
            StartActivity (intent);
        }

        protected void EndRefreshingOnUIThread (object sender)
        {
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                if (mSwipeRefreshLayout.Refreshing) {
                    mSwipeRefreshLayout.Refreshing = false;
                }
            });
        }

        NcTimer refreshTimer;

        void rearmRefreshTimer (int seconds)
        {
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
            refreshTimer = new NcTimer ("MessageListFragment refresh", EndRefreshingOnUIThread, null, seconds * 1000, 0); 
        }

        void cancelRefreshTimer ()
        {
            if (mSwipeRefreshLayout.Refreshing) {
                EndRefreshingOnUIThread (null);
            }
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }

        public void DeleteThisMessage (McEmailMessageThread messageThread)
        {
            if (messages.HasOutboxSemantics ()) {
                EmailHelper.DeleteEmailThreadFromOutbox (messageThread);
                return;
            }
            if (messages.HasDraftsSemantics ()) {
                EmailHelper.DeleteEmailThreadFromDrafts (messageThread);
                return;
            }
            NcAssert.NotNull (messageThread);
            Log.Debug (Log.LOG_UI, "DeleteThisMessage");
            NcEmailArchiver.Delete (messageThread);
        }

        public void ArchiveThisMessage (McEmailMessageThread messageThread)
        {
            NcAssert.NotNull (messageThread);
            NcEmailArchiver.Archive (messageThread);
        }

        public void ShowPriorityChooser (McEmailMessageThread messageThread)
        {
            Console.WriteLine ("ShowPriorityChooser: {0}", messageThread);
            var deferralFragment = ChooseDeferralFragment.newInstance (messageThread);
            deferralFragment.setOnDeferralSelected (OnDeferralSelected);
            var ft = FragmentManager.BeginTransaction ();
            ft.AddToBackStack (null);
            deferralFragment.Show (ft, "dialog");
        }

        public void ShowFolderChooser (McEmailMessageThread messageThread)
        {
            Console.WriteLine ("ShowFolderChooser: {0}", messageThread);
            var folderFragment = ChooseFolderFragment.newInstance (messageThread);
            folderFragment.setOnFolderSelected (OnFolderSelected);
            var ft = FragmentManager.BeginTransaction ();
            ft.AddToBackStack (null);
            folderFragment.Show (ft, "dialog");
        }

        public void OnDeferralSelected (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate)
        {
            NcMessageDeferral.DateSelected (NcMessageDeferral.MessageDateType.Defer, thread, request, selectedDate);
        }

        public void OnFolderSelected (McFolder folder, McEmailMessageThread thread)
        {
            Console.WriteLine ("OnFolderSelected: {0}", thread);
            NcEmailArchiver.Move (thread, folder);
        }

        public void SwitchAccount (INachoEmailMessages newMessages)
        {
            messages = newMessages;
            messageListAdapter = new MessageListAdapter (messages);
            listView.Adapter = messageListAdapter;
        }

    }

    public class MessageListAdapter : Android.Widget.BaseAdapter<McEmailMessageThread>
    {
        INachoEmailMessages messages;

        public MessageListAdapter (INachoEmailMessages messages)
        {
            this.messages = messages;
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override long GetItemId (int position)
        {
            return messages.GetEmailThread (position).FirstMessageId;
        }

        public override int Count {
            get {
                return messages.Count ();
            }
        }

        public override McEmailMessageThread this [int position] {  
            get { return messages.GetEmailThread (position); }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageCell, parent, false);
                var chiliView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.chili);
                chiliView.Click += ChiliView_Click;
            }
            var thread = messages.GetEmailThread (position);
            var message = thread.FirstMessageSpecialCase ();
            Bind.BindMessageHeader (thread, message, view);

            // Preview label view
            var previewView = view.FindViewById<Android.Widget.TextView> (Resource.Id.preview);
            var cookedPreview = EmailHelper.AdjustPreviewText (message.GetBodyPreviewOrEmpty ());
            previewView.SetText (Android.Text.Html.FromHtml (cookedPreview), Android.Widget.TextView.BufferType.Spannable);

            var chiliTagView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.chili);
            chiliTagView.Tag = position;

            return view;
        }

        void ChiliView_Click (object sender, EventArgs e)
        {
            var chiliView = (Android.Widget.ImageView)sender;
            var position = (int)chiliView.Tag;
            var thread = messages.GetEmailThread (position);
            var message = thread.FirstMessageSpecialCase ();
            NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
            Bind.BindMessageChili (thread, message, chiliView);
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageScoreUpdated:
            case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
            case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                RefreshMessageIfVisible ();
                break;
            }
        }

        void RefreshMessageIfVisible ()
        {
            List<int> adds;
            List<int> deletes;
            if (messages.Refresh (out adds, out deletes)) {
                this.NotifyDataSetChanged ();
            }
        }


    }
}

