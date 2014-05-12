using Android.OS;
using Android.Views;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V7.App;
using Android.Support.V4.Widget;
using Android.Widget;
using NachoCore.Model;
using NachoCore;
using NachoClient;
using NachoCore.Utils;
using System;
using Android.App;

namespace NachoClient.AndroidClient
{
    public class FolderListFragment : Android.Support.V4.App.Fragment
    {

        INachoFolders folders;
        FolderListAdapter adapter;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var rootView = inflater.Inflate (Resource.Layout.FolderListFragment, container, false);
            var listview = rootView.FindViewById<ListView> (Resource.Id.listview);

            folders = new NachoFolders (NachoFolders.FilterForEmail);
            adapter = new FolderListAdapter (this.Activity, folders);
            listview.Adapter = adapter;

            // Watch for changes from the back end
            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_FolderSetChanged == s.Status.SubKind) {
                    folders.Refresh();
                    adapter.NotifyDataSetChanged();
                }
            };

            listview.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) => {
                var fragment = new MessageListFragment ();
                var bundle = new Bundle ();
                var folder = folders.GetFolder(e.Position);
                bundle.PutInt ("accountId", folder.AccountId);
                bundle.PutInt ("folderId", folder.Id);
                bundle.PutString ("segue", "FolderListToMessageList");
                fragment.Arguments = bundle;
                Activity.SupportFragmentManager.BeginTransaction ()
                    .Replace(Resource.Id.content_frame, fragment)
                    .Commit ();
            };

            Activity.Title = "Folders";
            return rootView;
        }
    }

    public class FolderListAdapter : BaseAdapter<McFolder>
    {
        Activity context;
        INachoFolders folders;

        public FolderListAdapter (Activity context, INachoFolders folders) : base ()
        {
            this.context = context;
            this.folders = folders;
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override McFolder this [int position] {  
            get { return folders.GetFolder (position); }
        }

        public override int Count {
            get { return folders.Count (); }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                // otherwise create a new one
                view = context.LayoutInflater.Inflate (Android.Resource.Layout.SimpleListItem1, null);
            }
            var folder = folders.GetFolder (position);
            view.FindViewById<TextView> (Android.Resource.Id.Text1).Text = folder.DisplayName;
            return view;
        }
    }
}
