//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Support.V4.App;
using Android.Support.Design.Widget;
using Android.Support.V7.Widget;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using NachoCore.ActiveSync;

namespace NachoClient.AndroidClient
{
    public class AllMailFragment : FolderListFragment, MainTabsActivity.Tab
    {


        #region Tab Interface

        public int TabMenuResource {
            get {
                return Resource.Menu.allmail;
            }
        }

        public void OnTabSelected (MainTabsActivity tabActivity)
        {
            if (Account.Id != NcApplication.Instance.Account.Id) {
                OnAccountSwitched (tabActivity);
            }
            tabActivity.ShowActionButton (Resource.Drawable.floating_action_new_mail_filled, ActionButtonClicked);
        }

        public void OnTabUnselected (MainTabsActivity tabActivity)
        {
        }

        public void OnAccountSwitched (MainTabsActivity tabActivity)
        {
            Account = NcApplication.Instance.Account;
            ReloadFolders ();
        }

        #endregion

        #region Subviews

        RecyclerView ListView;

        void FindSubviews (View view)
        {
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (view.Context));
        }

        void ClearSubviews ()
        {
            ListView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            Account = NcApplication.Instance.Account;
            return base.OnCreateView (inflater, container, savedInstanceState);
        }

        #endregion

        #region User Actions

        void ActionButtonClicked (object sender, EventArgs args)
        {
            ShowMessageCompose ();
        }

        #endregion

        #region Private Helpers 

        void ShowMessageCompose ()
        {
        	var intent = MessageComposeActivity.NewMessageIntent (Activity, NcApplication.Instance.Account.Id);
        	StartActivity (intent);
        }

        #endregion

    }

}
