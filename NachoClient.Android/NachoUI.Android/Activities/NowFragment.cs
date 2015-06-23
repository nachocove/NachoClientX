
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class NowFragment : Android.App.Fragment
    {
        Android.Widget.ImageView composeButton;
        Android.Widget.ImageView newMeetingButton;

        ViewPager pager;
        GenericFragmentPagerAdaptor adapter;

        public event EventHandler<McEmailMessageThread> onMessageClick;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.NowFragment, container, false);

            var activity = (NcActivity)this.Activity;
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

            pager = view.FindViewById<ViewPager> (Resource.Id.pager);
            adapter = new GenericFragmentPagerAdaptor (ChildFragmentManager);

            adapter.onMessageClick += Adapter_onMessageClick;

            pager.Adapter = adapter;

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();

            Console.WriteLine ("NowFragment: OnResume {0}", pager);
        }

        public override void OnPause ()
        {
            base.OnPause ();
            Console.WriteLine ("NowFragment: OnPause {0}", pager);
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
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(MessageComposeActivity));
            StartActivity (intent);
        }
    }


    public class GenericFragmentPagerAdaptor : Android.Support.V13.App.FragmentPagerAdapter
    {
        public event EventHandler<McEmailMessageThread> onMessageClick;

        INachoEmailMessages messages = NcEmailManager.PriorityInbox(NcApplication.Instance.Account.Id);

        public GenericFragmentPagerAdaptor (Android.App.FragmentManager fm) : base (fm)
        {
        }

        public override int Count {
            get { return messages.Count (); }
        }

        public override Android.App.Fragment GetItem (int position)
        {
            var thread = messages.GetEmailThread (position);
            var hotMessageFragment = new HotMessageFragment (thread);
            hotMessageFragment.onMessageClick += HotMessageFragment_onMessageClick;
            return hotMessageFragment;
        }

        void HotMessageFragment_onMessageClick (object sender, McEmailMessageThread thread)
        {
            if (null != onMessageClick) {
                onMessageClick (this, thread);
            }
        }

    }
   
}

