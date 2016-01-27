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
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class AttendeeListEditFragment : AttendeeListBaseFragment
    {
        private const int REMOVE_SWIPE_TAG = 1;
        private const int REQUIRED_SWIPE_TAG = 2;
        private const int OPTIONAL_SWIPE_TAG = 3;
        private const int SEND_INVITE_SWIPE_TAG = 4;

        private const int CONTACT_CHOOSER_REQUEST = 1;

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

            buttonBar.SetIconButton (ButtonBar.Button.Right1, Resource.Drawable.calendar_add_attendee, AddButton_Click);
            buttonBar.SetIconButton (ButtonBar.Button.Left1, Resource.Drawable.gen_close, CancelButton_Click);

            return view;
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);

            if (CONTACT_CHOOSER_REQUEST == requestCode && Result.Ok == resultCode && null != data) {
                string email;
                McContact contact;
                ContactEmailChooserActivity.GetSearchResults (data, out email, out contact);
                string name = contact == null ? email : contact.GetDisplayName ();
                var attendee = new McAttendee (accountId, name, email, CurrentTab.Optional == state ? NcAttendeeType.Optional : NcAttendeeType.Required);
                adapter.AddItem (attendee);
            }
        }

        protected override string EmptyListMessage ()
        {
            switch (state) {
            case CurrentTab.All:
                return "The meeting does not have any attendees. To add an attendee, tap the add button in the navigation bar above.";
            case CurrentTab.Required:
                return "The meeting does not have any required attendees. To add an attendee, tap the add button in the navigation bar above.";
            case CurrentTab.Optional:
                return "The meeting does not have any optional attendees. To add an attendee, tap the add button in the navigation bar above.";
            default:
                NcAssert.CaseError (string.Format ("Unexpected value for attendee view state: {0} ({1})", state.ToString (), (int)state));
                return "";
            }
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }

        private void SwipeMenu_Create (SwipeMenu menu)
        {
            if (REQUIRED_CELL_TYPE == menu.getViewType ()) {
                var optional = new SwipeMenuItem (this.Activity.ApplicationContext);
                optional.setBackground (A.Drawable_NachoSwipeAttendeeOptional (this.Activity));
                optional.setWidth (dp2px (90));
                optional.setTitle ("Optional");
                optional.setTitleSize (14);
                optional.setTitleColor (A.Color_White);
                optional.setIcon (A.Id_NachoSwipeAttendeeOptional);
                optional.setId (OPTIONAL_SWIPE_TAG);
                menu.addMenuItem (optional, SwipeMenu.SwipeSide.RIGHT);
            } else {
                var required = new SwipeMenuItem (this.Activity.ApplicationContext);
                required.setBackground (A.Drawable_NachoSwipeAttendeeRequired (this.Activity));
                required.setWidth (dp2px (90));
                required.setTitle ("Required");
                required.setTitleSize (14);
                required.setTitleColor (A.Color_White);
                required.setIcon (A.Id_NachoSwipeAttendeeRequired);
                required.setId (REQUIRED_SWIPE_TAG);
                menu.addMenuItem (required, SwipeMenu.SwipeSide.RIGHT);
            }

            var remove = new SwipeMenuItem (this.Activity.ApplicationContext);
            remove.setBackground (A.Drawable_NachoSwipeAttendeeRemove (this.Activity));
            remove.setWidth (dp2px (90));
            remove.setTitle ("Remove");
            remove.setTitleSize (14);
            remove.setTitleColor (A.Color_White);
            remove.setIcon (A.Id_NachoSwipeAttendeeRemove);
            remove.setId (REMOVE_SWIPE_TAG);
            menu.addMenuItem (remove, SwipeMenu.SwipeSide.RIGHT);

            var send = new SwipeMenuItem (this.Activity.ApplicationContext);
            send.setBackground (A.Drawable_NachoSwipeAttendeeResend (this.Activity));
            send.setWidth (dp2px (90));
            send.setTitle ("Send invite");
            send.setTitleSize (14);
            send.setTitleColor (A.Color_White);
            send.setIcon (A.Id_NachoSwipeAttendeeResend);
            send.setId (SEND_INVITE_SWIPE_TAG);
            menu.addMenuItem (send, SwipeMenu.SwipeSide.LEFT);
        }

        private bool SwipeMenu_Click (int position, SwipeMenu menu, int index)
        {
            switch (index) {
            case REMOVE_SWIPE_TAG:
                adapter.RemoveItem (position);
                break;
            case REQUIRED_SWIPE_TAG:
                adapter.MakeRequired (position);
                break;
            case OPTIONAL_SWIPE_TAG:
                adapter.MakeOptional (position);
                break;
            case SEND_INVITE_SWIPE_TAG:
                NcAlertView.ShowMessage (this.Activity, "Not yet implemented", "Resending the invite is not yet implemented. Please try again later.");
                break;
            }
            return false;
        }

        private void AddButton_Click (object sender, EventArgs e)
        {
            StartActivityForResult (ContactEmailChooserActivity.EmptySearchIntent (this.Activity), CONTACT_CHOOSER_REQUEST);
        }

        private void CancelButton_Click (object sender, EventArgs e)
        {
            this.Activity.SetResult (Result.Canceled);
            this.Activity.Finish ();
        }
    }
}

