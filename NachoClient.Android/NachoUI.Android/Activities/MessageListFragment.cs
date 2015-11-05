
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
using NachoPlatform;
using Android.Widget;
using Android.Views.InputMethods;

namespace NachoClient.AndroidClient
{
    public interface MessageListDelegate
    {
        bool ShowHotEvent ();

        void SetActiveImage (View view);
    }

    public class MessageListFragment : Fragment
    {
        private const int ARCHIVE_TAG = 1;
        private const int SAVE_TAG = 2;
        private const int DELETE_TAG = 3;
        private const int DEFER_TAG = 4;

        private const int LATE_TAG = 1;
        private const int FORWARD_TAG = 2;

        SwipeMenuListView listView;
        MessageListAdapter messageListAdapter;
        HotEventAdapter hotEventAdapter;

        SwipeRefreshLayout mSwipeRefreshLayout;

        bool searching;
        string searchToken;
        SearchHelper searcher;
        Android.Widget.EditText searchEditText;

        public INachoEmailMessages messages;
        public NachoMessageSearchResults searchResultsMessages;

        public bool multiSelectActive = false;
        public HashSet<long> MultiSelectSet = null;

        Android.Widget.ImageView leftButton1;
        Android.Widget.ImageView rightButton1;
        Android.Widget.ImageView rightButton2;
        Android.Widget.ImageView rightButton3;

        public event EventHandler<McEvent> onEventClick;
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

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            mSwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            mSwipeRefreshLayout.SetColorSchemeResources (Resource.Color.refresh_1, Resource.Color.refresh_2, Resource.Color.refresh_3);

            mSwipeRefreshLayout.Refresh += (object sender, EventArgs e) => {
                var nr = messages.StartSync ();
                rearmRefreshTimer (NachoSyncResult.DoesNotSync (nr) ? 3 : 10);
            };

            leftButton1 = view.FindViewById<Android.Widget.ImageView> (Resource.Id.left_button1);
            leftButton1.Click += LeftButton1_Click;

            rightButton1 = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            rightButton1.Click += RightButton1_Click;

            rightButton2 = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button2);
            rightButton2.Click += RightButton2_Click;

            rightButton3 = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button3);
            rightButton3.Click += RightButton3_Click;

            var cancelButton = view.FindViewById (Resource.Id.cancel);
            cancelButton.Click += CancelButton_Click;

            messageListAdapter = new MessageListAdapter (this);

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
            }
            );

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

            listView.setOnSwipeStartListener ((position) => {
                mSwipeRefreshLayout.Enabled = false;
            });

            listView.setOnSwipeEndListener ((position) => {
                mSwipeRefreshLayout.Enabled = true;
            });

            searchEditText = view.FindViewById<Android.Widget.EditText> (Resource.Id.searchstring);
            searchEditText.TextChanged += SearchString_TextChanged;

            searchResultsMessages = new NachoMessageSearchResults (NcApplication.Instance.Account.Id);

            searcher = new SearchHelper ("MessageListViewController", (searchString) => {
                if (String.IsNullOrEmpty (searchString)) {
                    searchResultsMessages.UpdateMatches (null);
                    searchResultsMessages.UpdateServerMatches (null);
                    messageListAdapter.RefreshSearchMatches ();
                    return; 
                }
                // On-device index
                int curVersion = searcher.Version;
                var indexPath = NcModel.Instance.GetIndexPath (NcApplication.Instance.Account.Id);
                var index = new NachoCore.Index.NcIndex (indexPath);
                int maxResults = 1000;
                if (4 > searchString.Length) {
                    maxResults = 20;
                }
                var matches = index.SearchAllEmailMessageFields (searchString, maxResults);

                // Cull low scores
                var maxScore = 0f;
                foreach (var m in matches) {
                    maxScore = Math.Max (maxScore, m.Score);
                }
                matches.RemoveAll (x => x.Score < (maxScore / 2));

                if (curVersion == searcher.Version) {
                    InvokeOnUIThread.Instance.Invoke (() => {
                        searchResultsMessages.UpdateMatches (matches);
                        messageListAdapter.RefreshSearchMatches ();
                    });
                }
            });
                
            var parent = (MessageListDelegate)Activity;
            var hotEvent = view.FindViewById<View> (Resource.Id.hot_event);

            if (parent.ShowHotEvent ()) {
                hotEvent.Visibility = ViewStates.Visible;
                var hoteventListView = view.FindViewById<SwipeMenuListView> (Resource.Id.hotevent_listView);
                hotEventAdapter = new HotEventAdapter ();
                hoteventListView.Adapter = hotEventAdapter;
                var hoteventEmptyView = view.FindViewById<View> (Resource.Id.hot_event_empty);
                hoteventListView.EmptyView = hoteventEmptyView;

                hoteventListView.ItemClick += HoteventListView_ItemClick;

                hoteventListView.setMenuCreator ((menu) => {
                    SwipeMenuItem lateItem = new SwipeMenuItem (Activity.ApplicationContext);
                    lateItem.setBackground (new ColorDrawable (A.Color_NachoSwipeCalendarLate));
                    lateItem.setWidth (dp2px (90));
                    lateItem.setTitle ("I'm Late");
                    lateItem.setTitleSize (14);
                    lateItem.setTitleColor (A.Color_White);
                    lateItem.setIcon (A.Id_NachoSwipeCalendarLate);
                    lateItem.setId (LATE_TAG);
                    menu.addMenuItem (lateItem, SwipeMenu.SwipeSide.LEFT);

                    SwipeMenuItem forwardItem = new SwipeMenuItem (Activity.ApplicationContext);
                    forwardItem.setBackground (new ColorDrawable (A.Color_NachoSwipeCalendarForward));
                    forwardItem.setWidth (dp2px (90));
                    forwardItem.setTitle ("Forward");
                    forwardItem.setTitleSize (14);
                    forwardItem.setTitleColor (A.Color_White);
                    forwardItem.setIcon (A.Id_NachoSwipeCalendarForward);
                    forwardItem.setId (FORWARD_TAG);
                    menu.addMenuItem (forwardItem, SwipeMenu.SwipeSide.RIGHT);
                });

                hoteventListView.setOnMenuItemClickListener (( position, menu, index) => {
                    var cal = CalendarHelper.GetMcCalendarRootForEvent (hotEventAdapter [position].Id);
                    switch (index) {
                    case LATE_TAG:
                        if (null != cal) {
                            var outgoingMessage = McEmailMessage.MessageWithSubject (NcApplication.Instance.Account, "Re: " + cal.GetSubject ());
                            outgoingMessage.To = cal.OrganizerEmail;
                            StartActivity (MessageComposeActivity.InitialTextIntent (this.Activity, outgoingMessage, "Running late."));
                        }
                        break;
                    case FORWARD_TAG:
                        if (null != cal) {
                            StartActivity (MessageComposeActivity.ForwardCalendarIntent (
                                this.Activity, cal.Id, McEmailMessage.MessageWithSubject (NcApplication.Instance.Account, "Fwd: " + cal.GetSubject ())));
                        }
                        break;
                    default:
                        throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action index {0}", index));
                    }
                    return false;
                });
            } else {
                hotEvent.Visibility = ViewStates.Gone;
            }

            ConfigureButtons ();
            parent.SetActiveImage (view);

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            CancelSearchIfActive ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        void HoteventListView_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            if (null != onEventClick) {
                var currentEvent = hotEventAdapter [0];
                if (null != currentEvent) {
                    onEventClick (this, currentEvent);
                }
            }
        }

        void ListView_ItemClick (object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (searchEditText.WindowToken, HideSoftInputFlags.NotAlways);
            if (multiSelectActive) {
                if (MultiSelectSet.Contains (e.Position)) {
                    MultiSelectSet.Remove (e.Position);
                } else {
                    MultiSelectSet.Add (e.Position);
                }
                RefreshVisibleMessageCells ();
            } else {
                if (null != onMessageClick) {
                    onMessageClick (this, messageListAdapter [e.Position]);
                }
            }
        }


        // Search or disable multi-select (multi-select)
        void LeftButton1_Click (object sender, EventArgs e)
        {
            if (multiSelectActive) {
                MultiSelectCancel ();
            } else {
                SearchButton_Click (sender, e);
            }
        }

        // Compose or delete (multi-select)
        void RightButton1_Click (object sender, EventArgs e)
        {
            if (multiSelectActive) {
                MultiSelectDelete ();
            } else {
                StartActivity (MessageComposeActivity.NewMessageIntent (this.Activity));
            }
        }

        // Enable multi-select or save to folder (multi-select)
        void RightButton2_Click (object sender, EventArgs e)
        {
            if (multiSelectActive) {
                ShowFolderChooser (null);
            } else {
                MultiSelectSet = new HashSet<long> ();
                multiSelectActive = true;
                ConfigureButtons ();
            }
        }

        // Blank or archive (multi-select)
        void RightButton3_Click (object sender, EventArgs e)
        {
            if (multiSelectActive) {
                MultiSelectArchive ();
            }
        }

        void ConfigureButtons ()
        {
            if (multiSelectActive) {
                listView.EnableSwipe (false);
                leftButton1.SetImageResource (Resource.Drawable.gen_close);
                leftButton1.Visibility = ViewStates.Visible;
                rightButton1.SetImageResource (Resource.Drawable.gen_delete_all);
                rightButton1.Visibility = ViewStates.Visible;
                rightButton2.SetImageResource (Resource.Drawable.folder_move);
                rightButton2.Visibility = ViewStates.Visible;
                rightButton3.SetImageResource (Resource.Drawable.gen_archive);
                rightButton3.Visibility = ViewStates.Visible;
            } else {
                listView.EnableSwipe (true);
                leftButton1.SetImageResource (Resource.Drawable.nav_search);
                leftButton1.Visibility = ViewStates.Visible;
                rightButton1.SetImageResource (Resource.Drawable.contact_newemail);
                rightButton1.Visibility = ViewStates.Visible;
                rightButton2.SetImageResource (Resource.Drawable.folder_edit);
                rightButton2.Visibility = ViewStates.Visible;
                rightButton3.Visibility = ViewStates.Invisible;
            }
            RefreshVisibleMessageCells ();
        }

        void MultiSelectCancel ()
        {
            multiSelectActive = false;
            MultiSelectSet = null;
            ConfigureButtons ();
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

        void SearchButton_Click (object sender, EventArgs e)
        {
            StartSearching ();
        }


        void CancelButton_Click (object sender, EventArgs e)
        {
            if (searching) {
                CancelSearch ();
            }
        }

        void StartSearching ()
        {
            searching = true;
            messageListAdapter.StartSearch ();

            var search = View.FindViewById (Resource.Id.search);
            search.Visibility = ViewStates.Visible;
            var navbar = View.FindViewById (Resource.Id.navigation_bar);
            navbar.Visibility = ViewStates.Gone;
            var navtoolbar = View.FindViewById (Resource.Id.navigation_toolbar);
            navtoolbar.Visibility = ViewStates.Gone;

            var parent = (MessageListDelegate)Activity;
            var hotEvent = View.FindViewById<View> (Resource.Id.hot_event);
            if (parent.ShowHotEvent ()) {
                hotEvent.Visibility = ViewStates.Gone;
            }

            searchEditText.RequestFocus ();
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.ShowSoftInput (searchEditText, ShowFlags.Implicit);
        }

        void CancelSearch ()
        {
            searching = false;
            messageListAdapter.CancelSearch ();

            searchEditText.ClearFocus ();
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (searchEditText.WindowToken, HideSoftInputFlags.NotAlways);
            searchEditText.Text = "";

            var navbar = View.FindViewById (Resource.Id.navigation_bar);
            navbar.Visibility = ViewStates.Visible;
            var navtoolbar = View.FindViewById (Resource.Id.navigation_toolbar);
            navtoolbar.Visibility = ViewStates.Visible;
            var search = View.FindViewById (Resource.Id.search);
            search.Visibility = ViewStates.Gone;

            var parent = (MessageListDelegate)Activity;
            var hotEvent = View.FindViewById<View> (Resource.Id.hot_event);
            if (parent.ShowHotEvent ()) {
                hotEvent.Visibility = ViewStates.Visible;
            }
        }

        protected void CancelSearchIfActive ()
        {
            if (!String.IsNullOrEmpty (searchToken)) {
                McPending.Cancel (NcApplication.Instance.Account.Id, searchToken);
                searchToken = null;
            }
        }

        void SearchString_TextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            if (String.IsNullOrEmpty (searchEditText.Text)) {
                searchResultsMessages.UpdateServerMatches (null);
                messageListAdapter.RefreshSearchMatches ();
            } else {
                // Ask the server
                KickoffSearchApi (0, searchEditText.Text);
            }
            searcher.Search (searchEditText.Text);
        }

        protected void KickoffSearchApi (int forSearchOption, string forSearchString)
        {
            if (String.IsNullOrEmpty (forSearchString) || (4 > forSearchString.Length)) {
                searchResultsMessages.UpdateServerMatches (null);
                messageListAdapter.RefreshSearchMatches ();
                return;
            }
            if (String.IsNullOrEmpty (searchToken)) {
                searchToken = BackEnd.Instance.StartSearchEmailReq (NcApplication.Instance.Account.Id, forSearchString, null).GetValue<string> ();
            } else {
                BackEnd.Instance.SearchEmailReq (NcApplication.Instance.Account.Id, forSearchString, null, searchToken);
            }
        }

        protected void UpdateSearchResultsFromServer (List<NcEmailMessageIndex> indexList)
        {
            var threadList = new List<McEmailMessageThread> ();
            foreach (var i in indexList) {
                var thread = new McEmailMessageThread ();
                thread.FirstMessageId = i.Id;
                thread.MessageCount = 1;
                threadList.Add (thread);
            }
            searchResultsMessages.UpdateServerMatches (threadList);
            messageListAdapter.RefreshSearchMatches ();
        }

        public void OnBackPressed ()
        {
            if (searching) {
                CancelSearch ();
            }
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

        public void MultiSelectDelete ()
        {
            var messageList = GetSelectedMessages ();
            NcEmailArchiver.Delete (messageList);
            MultiSelectCancel ();
        }

        public void MultiSelectMove (McFolder folder)
        {
            var messageList = GetSelectedMessages ();
            NcEmailArchiver.Move (messageList, folder);
            MultiSelectCancel ();
        }

        public void MultiSelectArchive ()
        {
            var messageList = GetSelectedMessages ();
            NcEmailArchiver.Archive (messageList);
            MultiSelectCancel ();
        }

        public void ShowPriorityChooser (McEmailMessageThread messageThread)
        {
            Log.Info (Log.LOG_UI, "ShowPriorityChooser: {0}", messageThread);
            var deferralFragment = ChooseDeferralFragment.newInstance (messageThread);
            deferralFragment.setOnDeferralSelected (OnDeferralSelected);
            var ft = FragmentManager.BeginTransaction ();
            ft.AddToBackStack (null);
            deferralFragment.Show (ft, "dialog");
        }

        public void ShowFolderChooser (McEmailMessageThread messageThread)
        {
            Log.Info (Log.LOG_UI, "ShowFolderChooser: {0}", messageThread);
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
            Log.Info (Log.LOG_UI, "OnFolderSelected: {0}", thread);
            if (multiSelectActive) {
                MultiSelectMove (folder);
            } else {
                NcEmailArchiver.Move (thread, folder);
            }
        }

        public void SwitchAccount (INachoEmailMessages newMessages)
        {
            messages = newMessages;
            messageListAdapter = new MessageListAdapter (this);
            listView.Adapter = messageListAdapter;
        }

        void RefreshVisibleMessageCells ()
        {
            for (var i = listView.FirstVisiblePosition; i <= listView.LastVisiblePosition; i++) {
                var cell = listView.GetChildAt (i - listView.FirstVisiblePosition);
                if (null != cell) {
                    messageListAdapter.GetView (i, cell, listView);
                }
            }
        }

        public List<McEmailMessage> GetSelectedMessages ()
        {
            var messageList = new List<McEmailMessage> ();

            foreach (var messageThreadIndex in MultiSelectSet) {
                var messageThread = messages.GetEmailThread ((int)messageThreadIndex);
                foreach (var message in messageThread) {
                    messageList.Add (message);
                }
            }
            return messageList;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_EmailMessageChanged:
            case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
            case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                RefreshVisibleMessageCells ();
                break;
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageScoreUpdated:
                RefreshIfVisible ();
                break;
            case NcResult.SubKindEnum.Info_EmailSearchCommandSucceeded:
                UpdateSearchResultsFromServer (s.Status.GetValue<List<NcEmailMessageIndex>> ());
                break;
            }
        }

        public void RefreshIfVisible ()
        {
            List<int> adds;
            List<int> deletes;
            if (messages.Refresh (out adds, out deletes)) {
                messageListAdapter.NotifyDataSetChanged ();
            }
        }

    }

    public class MessageListAdapter : Android.Widget.BaseAdapter<McEmailMessageThread>
    {
        MessageListFragment owner;

        bool searching;

        public MessageListAdapter (MessageListFragment owner)
        {
            this.owner = owner;

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

        public override long GetItemId (int position)
        {
            if (searching) {
                return owner.searchResultsMessages.GetEmailThread (position).FirstMessageId;
            } else {
                return owner.messages.GetEmailThread (position).FirstMessageId;
            }
        }

        public override int Count {
            get {
                if (searching) {
                    return owner.searchResultsMessages.Count ();
                } else {
                    return owner.messages.Count ();
                }
            }
        }

        public override McEmailMessageThread this [int position] {  
            get { 
                if (searching) {
                    return owner.searchResultsMessages.GetEmailThread (position);
                } else {
                    return owner.messages.GetEmailThread (position);
                }
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageCell, parent, false);
                var chiliView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.chili);
                chiliView.Click += ChiliView_Click;
            }
            McEmailMessageThread thread;
            if (searching) {
                thread = owner.searchResultsMessages.GetEmailThread (position);
            } else {
                thread = owner.messages.GetEmailThread (position);
            }
            var message = thread.FirstMessageSpecialCase ();
            Bind.BindMessageHeader (thread, message, view);

            // Preview label view
            var previewView = view.FindViewById<Android.Widget.TextView> (Resource.Id.preview);
            var cookedPreview = EmailHelper.AdjustPreviewText (message.GetBodyPreviewOrEmpty ());
            previewView.SetText (Android.Text.Html.FromHtml (cookedPreview), Android.Widget.TextView.BufferType.Spannable);

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

        void ChiliView_Click (object sender, EventArgs e)
        {
            var chiliView = (Android.Widget.ImageView)sender;
            var position = (int)chiliView.Tag;
            McEmailMessageThread thread;
            if (searching) {
                thread = owner.searchResultsMessages.GetEmailThread (position);
            } else {
                thread = owner.messages.GetEmailThread (position);
            }
            var message = thread.FirstMessageSpecialCase ();
            NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
            Bind.BindMessageChili (thread, message, chiliView);
        }

    }

    public class HotEventAdapter : Android.Widget.BaseAdapter<McEvent>
    {
        protected McEvent currentEvent;
        protected NcTimer eventEndTimer = null;

        public HotEventAdapter ()
        {
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            Configure ();
        }

        public void Configure ()
        {
            DateTime timerFireTime;
            currentEvent = CalendarHelper.CurrentOrNextEvent (out timerFireTime);

            if (null != eventEndTimer) {
                eventEndTimer.Dispose ();
                eventEndTimer = null;
            }

            // Set a timer to fire at the end of the currently displayed event, so the view can
            // be reconfigured to show the next event.
            if (null != currentEvent) {
                TimeSpan timerDuration = timerFireTime - DateTime.UtcNow;
                if (timerDuration < TimeSpan.Zero) {
                    // The time to reevaluate the current event was in the very near future, and that time was reached in between
                    // CurrentOrNextEvent() and now.  Configure the timer to fire immediately.
                    timerDuration = TimeSpan.Zero;
                }
                eventEndTimer = new NcTimer ("HotEventView", (state) => {
                    InvokeOnUIThread.Instance.Invoke (() => {
                        Configure ();
                    });
                }, null, timerDuration, TimeSpan.Zero);
            }
        }

        public override long GetItemId (int position)
        {
            return 1;
        }

        public override int Count {
            get {
                return (null == currentEvent ? 0 : 1);
            }
        }

        public override McEvent this [int position] {  
            get { return currentEvent; }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.HotEventCell, parent, false);
            }
            Bind.BindHotEvent (currentEvent, view);

            return view;
        }


        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;

            switch (statusEvent.Status.SubKind) {

            case NcResult.SubKindEnum.Info_EventSetChanged:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                Configure ();
                NotifyDataSetChanged ();
                break;

            case NcResult.SubKindEnum.Info_ExecutionContextChanged:
                // When the app goes into the background, eventEndTimer might get cancelled, but ViewWillAppear
                // won't get called when the app returns to the foreground.  That might leave the view displaying
                // an old event.  Watch for foreground events and refresh the view.
                if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                    Configure ();
                    NotifyDataSetChanged ();
                }
                break;
            }
        }

    }
}

