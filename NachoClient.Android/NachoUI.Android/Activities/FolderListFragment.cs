
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class FolderListFragment : Fragment
    {
        FolderListAdapter folderListAdapter;

        public delegate void OnFolderSelectedListener (McFolder folder);

        public OnFolderSelectedListener onFolderSelected;

        public static FolderListFragment newInstance ()
        {
            var fragment = new FolderListFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.FolderListFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            var listview = view.FindViewById<ListView> (Resource.Id.listView);
            folderListAdapter = new FolderListAdapter (view.Context);
            folderListAdapter.setOnFolderSelected (OnFolderSelected);

            listview.Adapter = folderListAdapter;
            return view;
        }

        void OnFolderSelected (McFolder folder)
        {
            if (null != onFolderSelected) {
                onFolderSelected (folder);
            }
        }

        public void SwitchAccount()
        {
            if (null != folderListAdapter) {
                folderListAdapter.SwitchAccount ();
            }
        }
    }


    public class FolderListAdapter : BaseAdapter
    {
        Context context;
        LayoutInflater inflater;

        NachoCore.NachoFolders Folders;

        FolderListFragment.OnFolderSelectedListener onFolderSelected;

        public FolderListAdapter (Context c)
        {
            context = c;
            inflater = (LayoutInflater)context.GetSystemService (Context.LayoutInflaterService);
            Folders = new NachoFolders (NcApplication.Instance.Account.Id, NachoFolders.FilterForEmail);
        }

        public void SwitchAccount()
        {
            Folders = new NachoFolders (NcApplication.Instance.Account.Id, NachoFolders.FilterForEmail);
            this.NotifyDataSetChanged ();
        }

        public void setOnFolderSelected (FolderListFragment.OnFolderSelectedListener onFolderSelected)
        {
            this.onFolderSelected = onFolderSelected;
        }

        public override int Count {
            get { return Folders.Count (); }
        }

        public override Java.Lang.Object GetItem (int position)
        {
            return null;
        }

        public override long GetItemId (int position)
        {
            return 0;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view;
            if (convertView == null) {
                view = inflater.Inflate (Resource.Layout.FolderCell, null);
            } else {
                view = convertView;
            }

            var label = view.FindViewById<TextView> (Resource.Id.label);
            label.Text = Folders.GetFolder (position).DisplayName;

            view.Click += View_Click;
            view.Tag = position;
            return view;
        }

        void View_Click (object sender, EventArgs e)
        {
            var view = (View)sender;
            var position = (int)view.Tag;

            if (null != onFolderSelected) {
                onFolderSelected (Folders.GetFolder (position));
            }
        }
    }
}

