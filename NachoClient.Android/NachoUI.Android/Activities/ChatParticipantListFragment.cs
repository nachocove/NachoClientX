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
    public class ChatParticipantListFragment : Fragment
    {
        private const int REMOVE_SWIPE_TAG = 1;

        private const int CONTACT_CHOOSER_REQUEST = 1;

        protected int accountId;
        protected IList<McChatParticipant> participants;

        protected ButtonBar buttonBar;
        protected ParticipantListAdapter adapter;

        public IList<McChatParticipant> Participants {
            get {
                return participants;
            }
            set {
                participants = value;
            }
        }

        public int AccountId {
            set {
                accountId = value;
            }
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            adapter = new ParticipantListAdapter ();
            adapter.AccountId = accountId;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = base.OnCreateView (inflater, container, savedInstanceState);

            var listView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);

            listView.setMenuCreator (SwipeMenu_Create);
            listView.setOnMenuItemClickListener (SwipeMenu_Click);

            buttonBar.SetIconButton (ButtonBar.Button.Right1, Resource.Drawable.chat_add_contact, AddButton_Click);
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
                // adapter.AddItem (attendee);
            }
        }

        protected string EmptyListMessage ()
        {
            return "The chat does not have any participants. To add an attendee, tap the add button in the navigation bar above.";
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }

        private void SwipeMenu_Create (SwipeMenu menu)
        {
            var remove = new SwipeMenuItem (this.Activity.ApplicationContext);
            remove.setBackground (A.Drawable_NachoSwipeAttendeeRemove (this.Activity));
            remove.setWidth (dp2px (90));
            remove.setTitle ("Remove");
            remove.setTitleSize (14);
            remove.setTitleColor (A.Color_White);
            remove.setIcon (A.Id_NachoSwipeAttendeeRemove);
            remove.setId (REMOVE_SWIPE_TAG);
            menu.addMenuItem (remove, SwipeMenu.SwipeSide.RIGHT);
        }

        private bool SwipeMenu_Click (int position, SwipeMenu menu, int index)
        {
            switch (index) {
            case REMOVE_SWIPE_TAG:
                adapter.RemoveItem (position);
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


    public class ParticipantListAdapter : BaseAdapter<McChatParticipant>
    {

        private List<McChatParticipant> participants;

        private Action changedCallback;

        private int accountId;

        public List<McChatParticipant> Participants {
            get {
                return participants;
            }
            set {
                participants = value;
            }
        }

        public ParticipantListAdapter (Action changedCallback = null)
        {
            this.changedCallback = changedCallback;
            participants = new List<McChatParticipant> ();
        }

        public int AccountId {
            set {
                accountId = value;
            }
        }

        public void AddItem (McChatParticipant participant)
        {
            participants.Add (participant);
        }

        public void RemoveItem (int position)
        {
            participants.RemoveAt (position);
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override int Count {
            get {
                return participants.Count;
            }
        }

        public override McChatParticipant this [int index] {
            get {
                return participants [index];
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View cell = convertView;
            if (null == cell) {
                cell = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.ChatParticipantListCell, parent, false);
            }
            var participant = this [position];
            var initialsView = cell.FindViewById<ContactPhotoView> (Resource.Id.user_image);
            var nameView = cell.FindViewById<TextView> (Resource.Id.attendee_name);
            var emailView = cell.FindViewById<TextView> (Resource.Id.attendee_email);
            var initials = ContactsHelper.NameToLetters (participant.CachedName);
            var color = Util.ColorResourceForEmail (accountId, participant.EmailAddress);
            initialsView.SetEmailAddress (accountId, participant.EmailAddress, initials, color);
            nameView.Text = participant.CachedName;
            emailView.Text = participant.EmailAddress;
            return cell;
        }
    }
}

