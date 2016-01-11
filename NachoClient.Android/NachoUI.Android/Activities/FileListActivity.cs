using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "FileListActivity")]
    public class FileListActivity : NcTabBarActivity
    {
        private const string FILE_LIST_FRAGMENT_TAG = "FileListFragment";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.FileListActivity);

            if (null == bundle || null == FragmentManager.FindFragmentByTag<FileListFragment> (FILE_LIST_FRAGMENT_TAG)) {
                var fileListFragment = FileListFragment.newInstance (NcApplication.Instance.Account.Id);
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, fileListFragment, FILE_LIST_FRAGMENT_TAG).Commit ();
            }
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

    }
}
