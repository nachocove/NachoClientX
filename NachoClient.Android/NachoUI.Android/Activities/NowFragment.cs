
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
using Android.Graphics.Drawables;
using Android.Widget;

namespace NachoClient.AndroidClient
{
    public class NowFragment : NcFragment
    {
        McAccount account;

        Android.Widget.ImageView composeButton;
        Android.Widget.ImageView newMeetingButton;

        ViewPager pager;
        PriorityInboxPagerAdaptor adapter;

        private const int LATE_TAG = 1;
        private const int FORWARD_TAG = 2;

        HotEventAdapter hotEventAdapter;

        public event EventHandler<McEvent> onEventClick;
        public event EventHandler<INachoEmailMessages> onThreadClick;
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

            var hotEvent = view.FindViewById<View> (Resource.Id.hot_event);

            hotEvent.Visibility = ViewStates.Visible;
            var hoteventListView = view.FindViewById<SwipeMenuListView> (Resource.Id.hotevent_listView);
            hotEventAdapter = new HotEventAdapter ();
            hoteventListView.Adapter = hotEventAdapter;
            var hoteventEmptyView = view.FindViewById<View> (Resource.Id.hot_event_empty);
            hoteventListView.EmptyView = hoteventEmptyView;

            hoteventListView.ItemClick += HoteventListView_ItemClick;

            hoteventListView.setMenuCreator ((menu) => {
                SwipeMenuItem lateItem = new SwipeMenuItem (Activity.ApplicationContext);
                lateItem.setBackground (new ColorDrawable (A.Color_NachoSwipeCalendarLate));
                lateItem.setWidth (dp2px (90));
                lateItem.setTitle ("I'm Late");
                lateItem.setTitleSize (14);
                lateItem.setTitleColor (A.Color_White);
                lateItem.setIcon (A.Id_NachoSwipeCalendarLate);
                lateItem.setId (LATE_TAG);
                menu.addMenuItem (lateItem, SwipeMenu.SwipeSide.LEFT);

                SwipeMenuItem forwardItem = new SwipeMenuItem (Activity.ApplicationContext);
                forwardItem.setBackground (new ColorDrawable (A.Color_NachoSwipeCalendarForward));
                forwardItem.setWidth (dp2px (90));
                forwardItem.setTitle ("Forward");
                forwardItem.setTitleSize (14);
                forwardItem.setTitleColor (A.Color_White);
                forwardItem.setIcon (A.Id_NachoSwipeCalendarForward);
                forwardItem.setId (FORWARD_TAG);
                menu.addMenuItem (forwardItem, SwipeMenu.SwipeSide.RIGHT);
            });

            hoteventListView.setOnMenuItemClickListener (( position, menu, index) => {
                var cal = CalendarHelper.GetMcCalendarRootForEvent (hotEventAdapter [position].Id);
                switch (index) {
                case LATE_TAG:
                    if (null != cal) {
                        var outgoingMessage = McEmailMessage.MessageWithSubject (NcApplication.Instance.Account, "Re: " + cal.GetSubject ());
                        outgoingMessage.To = cal.OrganizerEmail;
                        StartActivity (MessageComposeActivity.InitialTextIntent (this.Activity, outgoingMessage, "Running late."));
                    }
                    break;
                case FORWARD_TAG:
                    if (null != cal) {
                        StartActivity (MessageComposeActivity.ForwardCalendarIntent (
                            this.Activity, cal.Id, McEmailMessage.MessageWithSubject (NcApplication.Instance.Account, "Fwd: " + cal.GetSubject ())));
                    }
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action index {0}", index));
                }
                return false;
            });


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

        void Adapter_onThreadClick (object sender, INachoEmailMessages threadMessages)
        {
            if (null != onThreadClick) {
                onThreadClick (this, threadMessages);
            }
        }

        void Adapter_onMessageClick (object sender, McEmailMessageThread thread)
        {
            if (null != onMessageClick) {
                onMessageClick (this, thread);
            }
        }

        void HoteventListView_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            if (null != onEventClick) {
                var currentEvent = hotEventAdapter [0];
                if (null != currentEvent) {
                    onEventClick (this, currentEvent);
                }
            }
        }

        void NewMeetingButton_Click (object sender, EventArgs e)
        {
            StartActivity (EventEditActivity.NewEventIntent (this.Activity));
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
            adapter.onThreadClick += Adapter_onThreadClick;
            pager.Adapter = adapter;
        }

        int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }
    }

    public class PriorityInboxPagerAdaptor : Android.Support.V13.App.FragmentStatePagerAdapter
    {
        public event EventHandler<INachoEmailMessages> onThreadClick;
        public event EventHandler<McEmailMessageThread> onMessageClick;

        INachoEmailMessages messages = NcEmailSingleton.PrioritySingleton (NcApplication.Instance.Account.Id);

        public PriorityInboxPagerAdaptor (Android.App.FragmentManager fm) : base (fm)
        {
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override int Count {
            get { return messages.Count (); }
        }

        public override int GetItemPosition (Java.Lang.Object objectValue)
        {
            return PositionNone;
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
            if (1 == thread.MessageCount) {
                if (null != onMessageClick) {
                    onMessageClick (this, thread);
                }
            } else {
                var threadMessages = messages.GetAdapterForThread (thread.GetThreadId ());
                if (null != onThreadClick) {
                    onThreadClick (this, threadMessages);
                }
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

