
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class NowFragment : NcFragment
    {
        McAccount account;

        Android.Widget.ImageView composeButton;
        Android.Widget.ImageView newMeetingButton;

        ViewPager pager;
        PriorityInboxPagerAdaptor adapter;

        public event EventHandler<McEmailMessageThread> onMessageClick;

        // Pages thru hot messages
        public static NowFragment newInstance ()
        {
            var fragment = new NowFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.NowFragment, container, false);
            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);
            composeButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            composeButton.SetImageResource (Resource.Drawable.contact_newemail);
            composeButton.Visibility = Android.Views.ViewStates.Visible;
            composeButton.Click += ComposeButton_Click;

            newMeetingButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button2);
            newMeetingButton.SetImageResource (Resource.Drawable.cal_add);
            newMeetingButton.Visibility = Android.Views.ViewStates.Visible;
            newMeetingButton.Click += NewMeetingButton_Click;

            // Highlight the tab bar icon of this activity
            var hotImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.hot_image);
            hotImage.SetImageResource (Resource.Drawable.nav_nachonow_active);

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            MaybeSwitchAccount ();
        }

        public override void OnPause ()
        {
            base.OnPause ();
        }

        void Adapter_onMessageClick (object sender, McEmailMessageThread thread)
        {
            if (null != onMessageClick) {
                onMessageClick (this, thread);
            }
        }

        void NewMeetingButton_Click (object sender, EventArgs e)
        {

        }

        void ComposeButton_Click (object sender, EventArgs e)
        {
            StartActivity (MessageComposeActivity.NewMessageIntent (this.Activity));
        }

        public override void SwitchAccount ()
        {
            MaybeSwitchAccount ();
        }

        void MaybeSwitchAccount ()
        {
            if ((null != account) && (NcApplication.Instance.Account.Id == account.Id)) {
                return;
            }
            account = NcApplication.Instance.Account;
            pager = View.FindViewById<ViewPager> (Resource.Id.pager);
            pager.Adapter = null; // Seems to be required
            adapter = new PriorityInboxPagerAdaptor (ChildFragmentManager);
            adapter.onMessageClick += Adapter_onMessageClick;
            pager.Adapter = adapter;
        }

    }

    public class PriorityInboxPagerAdaptor : Android.Support.V13.App.FragmentStatePagerAdapter
    {
        public event EventHandler<McEmailMessageThread> onMessageClick;

        INachoEmailMessages messages = NcEmailManager.PriorityInbox (NcApplication.Instance.Account.Id);

        public PriorityInboxPagerAdaptor (Android.App.FragmentManager fm) : base (fm)
        {
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override int Count {
            get { return messages.Count (); }
        }

        public override Android.App.Fragment GetItem (int position)
        {
            var thread = messages.GetEmailThread (position);
            var hotMessageFragment = HotMessageFragment.newInstance (thread);
            hotMessageFragment.onMessageClick += HotMessageFragment_onMessageClick;
            return hotMessageFragment;
        }

        void HotMessageFragment_onMessageClick (object sender, McEmailMessageThread thread)
        {
            if (null != onMessageClick) {
                onMessageClick (this, thread);
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageScoreUpdated:
            case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
            case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                RefreshPriorityInboxIfVisible ();
                break;
            }
        }

        void RefreshPriorityInboxIfVisible ()
        {
            List<int> adds;
            List<int> deletes;
            if (messages.Refresh (out adds, out deletes)) {
                this.NotifyDataSetChanged ();
            }
        }

    }
   
}

