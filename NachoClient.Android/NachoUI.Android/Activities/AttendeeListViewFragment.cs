//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

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
using Android.Graphics.Drawables;
using NachoCore.Model;
using NachoCore;

namespace NachoClient.AndroidClient
{
    public class AttendeeListViewFragment : AttendeeListBaseFragment
    {
        private const int EMAIL_SWIPE_TAG = 1;
        private const int DIAL_SWIPE_TAG = 2;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = base.OnCreateView (inflater, container, savedInstanceState);

            var listView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);

            listView.setMenuCreator (SwipeMenu_Create);
            listView.setOnMenuItemClickListener (SwipeMenu_Click);

            return view;
        }

        protected override string EmptyListMessage ()
        {
            if (0 == adapter.Attendees.Count) {
                // This method will get called for the required tab or the optional tab,
                // but it should never be called for an empty "all" tab.  The attendees
                // view shouldn't have been opened in view mode in that case.
                NachoCore.Utils.Log.Error (NachoCore.Utils.Log.LOG_UI,
                    "The attendees view was opened in view mode for a calendar item with no attendees.  That shouldn't happen.");
                return "This meeting does not have any attendees.";
            }
            switch (state) {
            case CurrentTab.Required:
                return "This meeting does not have any required attendees.";
            case CurrentTab.Optional:
                return "This meeting does not have any optional attendees.";
            }
            // Should never get here, but the compiler doesn't know that.
            return "This meeting does not have any attendees.";
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }

        private void SwipeMenu_Create (SwipeMenu menu)
        {
            var emailItem = new SwipeMenuItem (this.Activity.ApplicationContext);
            emailItem.setBackground (A.Drawable_NachoSwipeContactEmail (Activity));
            emailItem.setWidth (dp2px (90));
            emailItem.setTitle ("Email");
            emailItem.setTitleSize (14);
            emailItem.setTitleColor (A.Color_White);
            emailItem.setIcon (A.Id_NachoSwipeContactEmail);
            emailItem.setId (EMAIL_SWIPE_TAG);
            menu.addMenuItem (emailItem, SwipeMenu.SwipeSide.RIGHT);

            var dialItem = new SwipeMenuItem (this.Activity.ApplicationContext);
            dialItem.setBackground (A.Drawable_NachoSwipeContactCall (Activity));
            dialItem.setWidth (dp2px (90));
            dialItem.setTitle ("Dial");
            dialItem.setTitleSize (14);
            dialItem.setTitleColor (A.Color_White);
            dialItem.setIcon (A.Id_NachoSwipeContactCall);
            dialItem.setId (DIAL_SWIPE_TAG);
            menu.addMenuItem (dialItem, SwipeMenu.SwipeSide.LEFT);
        }

        private bool SwipeMenu_Click (int position, SwipeMenu menu, int index)
        {
            var attendee = adapter [position];
            switch (index) {
            case EMAIL_SWIPE_TAG:
                StartActivity (MessageComposeActivity.NewMessageIntent (this.Activity, accountId, attendee.Email));
                break;
            case DIAL_SWIPE_TAG:
                var contact = McContact.QueryByEmailAddress (accountId, attendee.Email).FirstOrDefault ();
                if (null != contact) {
                    Util.CallNumber (this.Activity, contact, null);
                }
                break;
            }
            return false;
        }
    }
}

