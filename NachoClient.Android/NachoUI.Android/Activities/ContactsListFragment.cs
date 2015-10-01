
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
    public class ContactsListFragment : Fragment
    {
        private const int CALL_TAG = 1;
        private const int EMAIL_TAG = 2;

        SwipeMenuListView listView;
        ContactsListAdapter contactsListAdapter;

        SwipeRefreshLayout mSwipeRefreshLayout;

        Android.Widget.ImageView addButton;

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

            var activity = (NcActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            mSwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            mSwipeRefreshLayout.SetColorSchemeResources (Resource.Color.refresh_1, Resource.Color.refresh_2, Resource.Color.refresh_3);

            mSwipeRefreshLayout.Refresh += (object sender, EventArgs e) => {
                rearmRefreshTimer (3);
            };

            addButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            addButton.SetImageResource (Android.Resource.Drawable.IcMenuAdd);
            addButton.Visibility = Android.Views.ViewStates.Visible;
            addButton.Click += AddButton_Click;

            // Highlight the tab bar icon of this activity
            var inboxImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.contacts_image);
            inboxImage.SetImageResource (Resource.Drawable.nav_contacts_active);

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
            });

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

        void ListView_ItemClick (object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            if (null != onContactClick) {
                onContactClick (this, contactsListAdapter [e.Position]);
            }
        }

        void AddButton_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
//            intent.SetClass (this.Activity, typeof(ContactEditActivity));
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

        Dictionary<int,int> viewTypeMap;

        public ContactsListAdapter ()
        {
            RefreshContactsIfVisible ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
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

        protected void RefreshContactsIfVisible ()
        {
            viewTypeMap = new Dictionary<int, int> ();
            recents = McContact.RicContactsSortedByRank (NcApplication.Instance.Account.Id, 5);
            contacts = McContact.AllContactsSortedByName (true);
            sections = ContactsBinningHelper.BinningContacts (ref contacts);
        }

        public override long GetItemId (int position)
        {
            if (recentsCount > position) {
                return recents [position].Id;
            } else if (contactsCount > 0) {
                return contacts [position - recentsCount].Id;
            } else {
                NcAssert.CaseError ();
                return 0;
            }
        }

        public override int Count {
            get {
                return recentsCount + contactsCount;
            }
        }

        public override McContact this [int position] {  
            get {
                var id = GetItemId (position);
                return McContact.QueryById<McContact> ((int)id);
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
            var viewType = Bind.BindContactCell (contact, view);
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

