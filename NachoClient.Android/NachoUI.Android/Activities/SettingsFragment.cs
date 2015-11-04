
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
using Android.Support.V7.Widget;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class SettingsFragment : Fragment
    {
        RecyclerView recyclerView;
        RecyclerView.LayoutManager layoutManager;
        AccountAdapter accountAdapter;

        public static SettingsFragment newInstance ()
        {
            var fragment = new SettingsFragment ();
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.SettingsFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            accountAdapter = new AccountAdapter (AccountAdapter.DisplayMode.SettingsListview);
            accountAdapter.AddAccount += AccountAdapter_AddAccount;
            accountAdapter.AccountSelected += AccountAdapter_AccountSelected;

            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.recyclerView);
            recyclerView.SetAdapter (accountAdapter);

            layoutManager = new LinearLayoutManager (this.Activity);
            recyclerView.SetLayoutManager (layoutManager);

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        void AccountAdapter_AccountSelected (object sender, McAccount account)
        {
            var parent = (SettingsActivity)Activity;
            parent.AccountSettingsSelected (account);
        }

        void AccountAdapter_AddAccount (object sender, EventArgs e)
        {
            var parent = (AccountListDelegate)Activity;
            parent.AddAccount ();
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_AccountSetChanged:
                accountAdapter.Refresh ();
                break;
            case NcResult.SubKindEnum.Info_AccountChanged:
                var activity = (NcTabBarActivity)this.Activity;
                activity.SetSwitchAccountButtonImage (View);
                break;
            }
        }

    }
}

