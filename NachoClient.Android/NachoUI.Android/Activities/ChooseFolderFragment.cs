
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
using Android.Support.V7.Widget;

namespace NachoClient.AndroidClient
{
    public class ChooseFolderFragment : DialogFragment
    {
        public delegate void OnFolderSelectedListener (McFolder folder, McEmailMessageThread messageThread);

        McEmailMessageThread messageThread;
        FolderListAdapter folderListAdapter;
        OnFolderSelectedListener OnFolderSelected;

        int accountId;

        // Null is ok; messageThread is just a cookie
        public static ChooseFolderFragment newInstance (int accountId, McEmailMessageThread messageThread)
        {
            var fragment = new ChooseFolderFragment ();
            fragment.SetMessageThread (messageThread);
            fragment.accountId = accountId;
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

            folderListAdapter = new FolderListAdapter (accountId, hideFakeFolders: true);
            var layoutManager = new LinearLayoutManager (Activity);

            var recyclerView = view.FindViewById<RecyclerView> (Resource.Id.recyclerView);
            recyclerView.SetAdapter (folderListAdapter);
            recyclerView.SetLayoutManager (layoutManager);

            folderListAdapter.OnFolderSelected += FolderListAdapter_OnFolderSelected;

            return view;
        }

        public void SetOnFolderSelected (OnFolderSelectedListener OnFolderSelected)
        {
            this.OnFolderSelected = OnFolderSelected;
        }

        void FolderListAdapter_OnFolderSelected (object sender, McFolder folder)
        {
            Dismiss ();
            if (null != OnFolderSelected) {
                OnFolderSelected (folder, this.messageThread);
                folder.UpdateSet_LastAccessed (DateTime.UtcNow);
            }
        }

        public override void OnPause ()
        {
            base.OnPause ();
            // There isn't a good place to store messageThread across a configuration change.
            // So don't even try.  Always dismiss the dialog so Android doesn't try to
            // recreate it.
            Dismiss ();
        }

    }

}

