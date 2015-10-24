
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
using Android.Views.InputMethods;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public class ContactsListFragment : Fragment
    {
        private const int CALL_TAG = 1;
        private const int EMAIL_TAG = 2;

        bool searching;
        Android.Widget.EditText searchEditText;
        View letterBar;
        SwipeMenuListView listView;
        ContactsListAdapter contactsListAdapter;

        SwipeRefreshLayout mSwipeRefreshLayout;

        public event EventHandler<McContact> onContactClick;

        public static ContactsListFragment newInstance ()
        {
            var fragment = new ContactsListFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ContactsListFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            mSwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            mSwipeRefreshLayout.SetColorSchemeResources (Resource.Color.refresh_1, Resource.Color.refresh_2, Resource.Color.refresh_3);

            mSwipeRefreshLayout.Refresh += (object sender, EventArgs e) => {
                rearmRefreshTimer (3);
            };

            var addButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            addButton.SetImageResource (Android.Resource.Drawable.IcMenuAdd);
            addButton.Visibility = Android.Views.ViewStates.Visible;
            addButton.Click += AddButton_Click;

            var searchButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.left_button1);
            searchButton.SetImageResource (Android.Resource.Drawable.IcMenuSearch);
            searchButton.Visibility = Android.Views.ViewStates.Visible;
            searchButton.Click += SearchButton_Click;

            searchEditText = view.FindViewById<Android.Widget.EditText> (Resource.Id.searchstring);
            searchEditText.TextChanged += SearchString_TextChanged;

            var cancelButton = view.FindViewById (Resource.Id.cancel);
            cancelButton.Click += CancelButton_Click;

            // Highlight the tab bar icon of this activity
            var inboxImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.contacts_image);
            inboxImage.SetImageResource (Resource.Drawable.nav_contacts_active);

            letterBar = view.FindViewById<View> (Resource.Id.letter_bar);
            var letterList = view.FindViewById<Android.Widget.LinearLayout> (Resource.Id.letter_list);

            var recentView = inflater.Inflate (Resource.Layout.Recent, null);
            letterList.AddView (recentView);
            recentView.Tag = 0;
            recentView.Click += Letterbox_Click;

            const string letters = "!ABCDEFGHIJKLMNOPQRSTUVWXYZ#";
            for (int i = 1; i < 28; i++) {
                var letterbox = inflater.Inflate (Resource.Layout.Letter, null);
                var letter = letterbox.FindViewById<Android.Widget.TextView> (Resource.Id.letter);
                letter.Text = letters [i].ToString ();
                letterList.AddView (letterbox);
                letterbox.Tag = i;
                letterbox.Click += Letterbox_Click;
            }

            contactsListAdapter = new ContactsListAdapter ();

            listView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.Adapter = contactsListAdapter;

            listView.ItemClick += ListView_ItemClick;

            listView.setMenuCreator ((menu) => {
                SwipeMenuItem dialItem = new SwipeMenuItem (Activity.ApplicationContext);
                dialItem.setBackground (new ColorDrawable (A.Color_NachoSwipeContactCall));
                dialItem.setWidth (dp2px (90));
                dialItem.setTitle ("Dial");
                dialItem.setTitleSize (14);
                dialItem.setTitleColor (A.Color_White);
                dialItem.setIcon (A.Id_NachoSwipeContactCall);
                dialItem.setId (CALL_TAG);
                menu.addMenuItem (dialItem, SwipeMenu.SwipeSide.LEFT);
                SwipeMenuItem emailItem = new SwipeMenuItem (Activity.ApplicationContext);
                emailItem.setBackground (new ColorDrawable (A.Color_NachoSwipeContactEmail));
                emailItem.setWidth (dp2px (90));
                emailItem.setTitle ("Email");
                emailItem.setTitleSize (14);
                emailItem.setTitleColor (A.Color_White);
                emailItem.setIcon (A.Id_NachoSwipeContactEmail);
                emailItem.setId (EMAIL_TAG);
                menu.addMenuItem (emailItem, SwipeMenu.SwipeSide.RIGHT);
            }
            );

            listView.setOnMenuItemClickListener (( position, menu, index) => {
                switch (index) {
                case CALL_TAG:
                    break;
                case EMAIL_TAG:
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action index {0}", index));
                }
                return false;
            });

            return view;
        }

        void Letterbox_Click (object sender, EventArgs e)
        {
            var letterbox = (View)sender;
            if (null != contactsListAdapter) {
                var position = contactsListAdapter.PositionForSection ((int)letterbox.Tag);
                listView.SmoothScrollToPositionFromTop (position, dp2px (10), 125);
            }
        }

        void ListView_ItemClick (object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            if (null != onContactClick) {
                InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
                imm.HideSoftInputFromWindow (searchEditText.WindowToken, HideSoftInputFlags.NotAlways);
                onContactClick (this, contactsListAdapter [e.Position]);
            }
        }

        void AddButton_Click (object sender, EventArgs e)
        {
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
            contactsListAdapter.StartSearch ();

            var search = View.FindViewById (Resource.Id.search);
            search.Visibility = ViewStates.Visible;
            var navbar = View.FindViewById (Resource.Id.navigation_bar);
            navbar.Visibility = ViewStates.Gone;
            var navtoolbar = View.FindViewById (Resource.Id.navigation_toolbar);
            navtoolbar.Visibility = ViewStates.Gone;
            letterBar.Visibility = ViewStates.Gone;

            searchEditText.RequestFocus ();
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.ShowSoftInput (searchEditText, ShowFlags.Implicit);
        }

        void CancelSearch ()
        {
            searching = false;
            contactsListAdapter.CancelSearch ();

            searchEditText.ClearFocus ();
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (searchEditText.WindowToken, HideSoftInputFlags.NotAlways);
            searchEditText.Text = "";

            letterBar.Visibility = ViewStates.Visible;
            var navbar = View.FindViewById (Resource.Id.navigation_bar);
            navbar.Visibility = ViewStates.Visible;
            var navtoolbar = View.FindViewById (Resource.Id.navigation_toolbar);
            navtoolbar.Visibility = ViewStates.Visible;
            var search = View.FindViewById (Resource.Id.search);
            search.Visibility = ViewStates.Gone;
        }

        void SearchString_TextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            contactsListAdapter.Search (searchEditText.Text);
        }

        public void OnBackPressed ()
        {
            if (searching) {
                CancelSearch ();
            }
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
            refreshTimer = new NcTimer ("ContactsListFragment refresh", EndRefreshingOnUIThread, null, seconds * 1000, 0); 
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

    }

    public class ContactsListAdapter : Android.Widget.BaseAdapter<McContact>
    {
        List<NcContactIndex> recents;
        List<NcContactIndex> contacts;
        ContactBin[] sections;
        bool multipleSections;

        bool searching;
        SearchHelper searcher;
        List<McContactEmailAddressAttribute> searchResults = null;

        Dictionary<int,int> viewTypeMap;

        public ContactsListAdapter ()
        {
            RefreshContactsIfVisible ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            searcher = new SearchHelper ("ContactsTableViewSourceUpdateSearchResults", (searchString) => {
                if (String.IsNullOrEmpty (searchString)) {
                    InvokeOnUIThread.Instance.Invoke (() => {
                        searchResults = new List<McContactEmailAddressAttribute> ();
                        RefreshContactsIfVisible ();
                    });
                } else {
                    var results = McContact.SearchIndexAllContacts (searchString);
                    InvokeOnUIThread.Instance.Invoke (() => {
                        searchResults = results;
                        RefreshContactsIfVisible ();
                    });
                }
            });
        }

        public int PositionForSection (int section)
        {
            if (0 == section) {
                return 0;
            }
            section -= 1;
            return sections [section].Start + recentsCount;
        }

        public void StartSearch ()
        {
            searching = true;
        }

        public void CancelSearch ()
        {
            if (searching) {
                searching = false;
                searchResults = null;
                RefreshContactsIfVisible ();
            }
        }

        public void Search (string searchString)
        {
            if (searching) {
                searcher.Search (searchString);
            }
        }

        int recentsCount {
            get {
                return (null == recents ? 0 : recents.Count);
            }
        }

        int contactsCount {
            get {
                return (null == contacts ? 0 : contacts.Count);
            }
        }

        int searchResultsCount {
            get {
                return (null == searchResults ? 0 : searchResults.Count);
            }
        }

        protected void RefreshContactsIfVisible ()
        {
            viewTypeMap = new Dictionary<int, int> ();
            if (searching) {
                recents = null;
                contacts = null;
            } else {
                recents = McContact.RicContactsSortedByRank (NcApplication.Instance.Account.Id, 5);
                contacts = McContact.AllContactsSortedByName (true);
                sections = ContactsBinningHelper.BinningContacts (ref contacts);
            }
            NotifyDataSetChanged ();
        }

        public override long GetItemId (int position)
        {
            if (searching) {
                return searchResults [position].Id;
            } else {
                if (recentsCount > position) {
                    return recents [position].Id;
                } else if (contactsCount > 0) {
                    return contacts [position - recentsCount].Id;
                } else {
                    NcAssert.CaseError ();
                    return 0;
                }
            }
        }

        string GetBinLabel (int position)
        {
            if (searching) {
                return null;
            }
            if (recentsCount > position) {
                return (0 == position ? "Recent" : null);
            }
            var index = position - recentsCount;
            foreach (var s in sections) {
                if (index == s.Start) {
                    return s.FirstLetter.ToString ();
                }
                if (index < s.Start) {
                    return null;
                }
            }
            return null;
        }

        public override int Count {
            get {
                if (searching) {
                    return searchResultsCount;
                } else {
                    return recentsCount + contactsCount;
                }
            }
        }

        public override McContact this [int position] {  
            get {
                if (searching) {
                    var contactEmailAttribute = searchResults [position];
                    var contact = contactEmailAttribute.GetContact ();
                    // alternateEmailAddress = contactEmailAttribute.Value;
                    return contact;
                } else {
                    var id = GetItemId (position);
                    return McContact.QueryById<McContact> ((int)id);
                }
            }
        }

        public override int GetItemViewType (int position)
        {
            int viewType;
            if (viewTypeMap.TryGetValue (position, out viewType)) {
                return viewType;
            } else {
                return 0;
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.ContactCell, parent, false);
            }

            var contact = this [position];
            var viewType = Bind.BindContactCell (contact, view, GetBinLabel (position), null);
            viewTypeMap [position] = viewType;

            return view;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ContactSetChanged:
                RefreshContactsIfVisible ();
                break;
            }
        }

    }
}

