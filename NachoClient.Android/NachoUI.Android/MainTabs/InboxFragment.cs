//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Support.V4.App;
using Android.Support.Design.Widget;
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
    public class InboxFragment : MessageListFragment, MainTabsActivity.Tab
    {

        private McAccount Account;

        #region Tab Interface

        public int TabMenuResource {
            get {
                return Resource.Menu.inbox;
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
            CancelSyncing ();
            // TODO: cancel editing (if active)
            // TODO: cancel row swiping (if active)

			SetEmailMessages (NcEmailManager.Inbox (Account.Id));

            UpdateFilterbar ();
            ReloadTable (); // to clear the table since the new Messages is empty
            HasLoadedOnce = false;

			SetNeedsReload ();
        }

        #endregion

        #region Fragment Lifecycle

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            Account = NcApplication.Instance.Account;
            SetEmailMessages (NcEmailManager.Inbox (Account.Id));
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            return base.OnCreateView (inflater, container, savedInstanceState);
        }

        public override void OnDestroyView ()
        {
            base.OnDestroyView ();
        }

        public override void OnDestroy ()
        {
            base.OnDestroy ();
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
