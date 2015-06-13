using System;
using System.Linq;
using Android.App;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using NachoClient;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class NachoNowFragment : Android.Support.V4.App.Fragment
    {
        public ViewPager pager;
        public NachoNowPagerAdapter adapter;
        public INachoFolders folders;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            this.HasOptionsMenu = true;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var rootView = inflater.Inflate (Resource.Layout.NachoNowFragment, container, false);

//            folders = new NachoFolders (NcApplication.Instance.Account.Id, NachoFolders.FilterForEmail);
//
//            adapter = new NachoNowPagerAdapter (this, this.FragmentManager);
//            pager = rootView.FindViewById<ViewPager> (Resource.Id.pager);
//            pager.Adapter = adapter;
//
//            var buttonFirst = rootView.FindViewById<Button> (Resource.Id.goto_first);
//            buttonFirst.Click += (object sender, EventArgs e) => {
//                pager.CurrentItem = 0;
//            };
//
//            var buttonLast = rootView.FindViewById<Button> (Resource.Id.goto_last);
//            buttonLast.Click += (object sender, EventArgs e) => {
//                var position = folders.Count () - 1;
//                pager.CurrentItem = position;
//            };
//
//            pager.CurrentItem = 0;

            return rootView;
        }
    }

    public class NachoNowPagerAdapter : Android.Support.V4.App.FragmentStatePagerAdapter //, Android.Support.V4.View.ViewPager.IOnPageChangeListener
    {
        protected NachoNowFragment owner;
        protected Android.Support.V4.App.FragmentManager fm;

        public NachoNowPagerAdapter (NachoNowFragment owner, Android.Support.V4.App.FragmentManager fm) : base (fm)
        {
            this.fm = fm;
            this.owner = owner;
        }

        public override int Count {
            get {
                return owner.folders.Count ();
            }
        }

        public override Android.Support.V4.App.Fragment GetItem (int position)
        {               
            var f = NachoNowPageFragment.newInstance (owner, owner.folders.GetFolder (position).Id);
            return f;
        }
    }

    public class NachoNowPageFragment : Android.Support.V4.App.ListFragment
    {
        McFolder folder;
        INachoEmailMessages messages;

        public static Android.Support.V4.App.Fragment newInstance (NachoNowFragment context, int folderId)
        {
            var f = new NachoNowPageFragment ();
            Bundle b = new Bundle ();
            b.PutInt ("folderId", folderId);
            f.Arguments = b;
            return f;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            var folderId = Arguments.GetInt ("folderId");
            folder = NcModel.Instance.Db.Get<McFolder> (folderId);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var v = inflater.Inflate (Resource.Layout.NachoNowPage, null);
            var tv = v.FindViewById<TextView> (Resource.Id.text);
            tv.Text = folder.DisplayName;
            messages = new NachoEmailMessages (folder);
            this.ListAdapter = new MessageListAdapter (this.Activity, messages);
            return v;
        }

        public override void OnListItemClick (ListView l, View v, int position, long id)
        {
            var fragment = new MessageListFragment ();
            var bundle = new Bundle ();
            var thread = messages.GetEmailThread (position);
            var message = thread.SingleMessageSpecialCase ();
            bundle.PutInt ("accountId", message.AccountId);
            bundle.PutInt ("messageId", message.Id);
            bundle.PutInt ("folderId", folder.Id);
            bundle.PutString ("segue", "NachoNowToMessageList");
            fragment.Arguments = bundle;
            Activity.SupportFragmentManager.BeginTransaction ()
                .Replace (Resource.Id.content_frame, fragment)
                .Commit ();
        }
    }
}
