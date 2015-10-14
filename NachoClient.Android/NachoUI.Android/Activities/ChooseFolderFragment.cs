
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

using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;
using NachoCore;

namespace NachoClient.AndroidClient
{
    public class ChooseFolderFragment : DialogFragment
    {
        FolderAdapter folderAdapter;
        McEmailMessageThread messageThread;

        public delegate void OnFolderSelectedListener (McFolder folder, McEmailMessageThread messageThread);

        OnFolderSelectedListener mOnFolderSelected;

        public static ChooseFolderFragment newInstance (McEmailMessageThread messageThread)
        {
            var fragment = new ChooseFolderFragment ();
            fragment.SetMessageThread (messageThread);
            return fragment;
        }

        // Null is ok; messageThread is just a cookie
        public void SetMessageThread (McEmailMessageThread messageThread)
        {
            this.messageThread = messageThread;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            Dialog.Window.RequestFeature (WindowFeatures.NoTitle);

            var view = inflater.Inflate (Resource.Layout.ChooseFolderFragment, container, false);

            var listview = view.FindViewById<ListView> (Resource.Id.listview);
            folderAdapter = new FolderAdapter (view.Context);
            folderAdapter.setOnFolderSelected (OnFolderSelected);

            listview.Adapter = folderAdapter;

            return view;
        }

        public void setOnFolderSelected (OnFolderSelectedListener onFolderSelected)
        {
            mOnFolderSelected = onFolderSelected;
        }

        void OnFolderSelected (McFolder folder, McEmailMessageThread thisIsNull)
        {
            Dismiss ();
            if (null != mOnFolderSelected) {
                mOnFolderSelected (folder, this.messageThread);
            }

        }
    }

    public class FolderAdapter : BaseAdapter
    {
        Context context;
        LayoutInflater inflater;

        NachoCore.NachoFolders Folders;

        ChooseFolderFragment.OnFolderSelectedListener mOnFolderSelected;

        public FolderAdapter (Context c)
        {
            context = c;
            inflater = (LayoutInflater)context.GetSystemService (Context.LayoutInflaterService);
            Folders = new NachoFolders (NcApplication.Instance.Account.Id, NachoFolders.FilterForEmail);
        }

        public void setOnFolderSelected (ChooseFolderFragment.OnFolderSelectedListener onFolderSelected)
        {
            mOnFolderSelected = onFolderSelected;
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
                view = inflater.Inflate (Resource.Layout.ChooseFolderItem, null);
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

            if (null != mOnFolderSelected) {
                mOnFolderSelected (Folders.GetFolder (position), null);
            }
        }
    }
}

