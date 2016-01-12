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
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.AndroidClient
{
    public abstract class AttendeeListBaseFragment : Fragment
    {
        public enum CurrentTab { All, Required, Optional }

        public const int REQUIRED_CELL_TYPE = 0;
        public const int OPTIONAL_CELL_TYPE = 1;
        public const int NUM_CELL_TYPES = 2;

        protected int accountId;
        protected AttendeeListAdapter adapter;
        protected ButtonBar buttonBar;

        private TextView allTab;
        private TextView requiredTab;
        private TextView optionalTab;

        protected CurrentTab state;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            adapter = new AttendeeListAdapter (CheckEmptyList);
            state = CurrentTab.All;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.AttendeeListFragment, container, false);

            buttonBar = new ButtonBar (view);

            buttonBar.SetTitle ("Attendees");

            allTab = view.FindViewById<TextView> (Resource.Id.attendee_list_tab_all);
            requiredTab = view.FindViewById<TextView> (Resource.Id.attendee_list_tab_required);
            optionalTab = view.FindViewById<TextView> (Resource.Id.attendee_list_tab_optional);

            allTab.Click += AllTab_Click;
            requiredTab.Click += RequiredTab_Click;
            optionalTab.Click += OptionalTab_Click;

            var listView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.Adapter = adapter;
            listView.ItemClick += ListView_ItemClick;


            HighlightTab (allTab);
            CheckEmptyList (view);

            return view;
        }

        public IList<McAttendee> Attendees {
            get {
                return adapter.Attendees;
            }
            set {
                adapter.Attendees = new List<McAttendee> (value);
            }
        }

        public int AccountId {
            set {
                accountId = value;
            }
        }

        protected abstract string EmptyListMessage ();

        private void CheckEmptyList (View view)
        {
            if (0 == adapter.Count) {
                view.FindViewById<View> (Resource.Id.listView).Visibility = ViewStates.Gone;
                var emptyMessage = view.FindViewById<TextView> (Resource.Id.attendee_list_empty_message);
                emptyMessage.Visibility = ViewStates.Visible;
                emptyMessage.Text = EmptyListMessage ();
            } else {
                view.FindViewById<View> (Resource.Id.listView).Visibility = ViewStates.Visible;
                view.FindViewById<View> (Resource.Id.attendee_list_empty_message).Visibility = ViewStates.Gone;
            }
        }

        private void CheckEmptyList ()
        {
            if (null != View) {
                CheckEmptyList (View);
            }
        }

        private void HighlightTab (TextView view)
        {
            view.SetTextColor (Android.Graphics.Color.White);
            view.SetBackgroundResource (Resource.Color.NachoGreen);
        }

        private void UnhighlightTab (TextView view)
        {
            view.SetTextColor (Resources.GetColor (Resource.Color.NachoGreen));
            view.SetBackgroundResource (Resource.Drawable.BlackBorder);
        }

        private void ListView_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            var contact = McContact.QueryByEmailAddress (accountId, adapter [e.Position].Email).FirstOrDefault ();
            if (null != contact) {
                StartActivity (ContactViewActivity.ShowContactIntent (this.Activity, contact));
            }
        }

        private void AllTab_Click (object sender, EventArgs e)
        {
            if (CurrentTab.All != state) {
                state = CurrentTab.All;
                adapter.SetState (CurrentTab.All);
                HighlightTab (allTab);
                UnhighlightTab (requiredTab);
                UnhighlightTab (optionalTab);
                CheckEmptyList ();
            }
        }

        private void RequiredTab_Click (object sender, EventArgs e)
        {
            if (CurrentTab.Required != state) {
                state = CurrentTab.Required;
                adapter.SetState (CurrentTab.Required);
                UnhighlightTab (allTab);
                HighlightTab (requiredTab);
                UnhighlightTab (optionalTab);
                CheckEmptyList ();
            }
        }

        private void OptionalTab_Click (object sender, EventArgs e)
        {
            if (CurrentTab.Optional != state) {
                state = CurrentTab.Optional;
                adapter.SetState (CurrentTab.Optional);
                UnhighlightTab (allTab);
                UnhighlightTab (requiredTab);
                HighlightTab (optionalTab);
                CheckEmptyList ();
            }
        }
    }

    public class AttendeeListAdapter : BaseAdapter<McAttendee>
    {
        private AttendeeListBaseFragment.CurrentTab state = AttendeeListBaseFragment.CurrentTab.All;

        private List<McAttendee> allAttendees;
        private List<McAttendee> required;
        private List<McAttendee> optional;

        private Action changedCallback;

        public List<McAttendee> Attendees {
            get {
                return allAttendees;
            }
            set {
                allAttendees = value;
                UpdateRequiredOptional ();
            }
        }

        public AttendeeListAdapter (Action changedCallback = null)
        {
            this.changedCallback = changedCallback;
            Attendees = new List<McAttendee> ();
        }

        public void SetState (AttendeeListBaseFragment.CurrentTab newState)
        {
            if (newState != state) {
                state = newState;
                NotifyDataSetChanged ();
            }
        }

        public void AddItem (McAttendee attendee)
        {
            allAttendees.Add (attendee);
            UpdateRequiredOptional ();
        }

        public void RemoveItem (int position)
        {
            if (AttendeeListBaseFragment.CurrentTab.All == state) {
                allAttendees.RemoveAt (position);
            } else {
                allAttendees.Remove (CollectionToShow () [position]);
            }
            UpdateRequiredOptional ();
        }

        public void MakeRequired (int position)
        {
            var attendee = CollectionToShow () [position];
            attendee.AttendeeTypeIsSet = true;
            attendee.AttendeeType = NcAttendeeType.Required;
            UpdateRequiredOptional ();
        }

        public void MakeOptional (int position)
        {
            var attendee = CollectionToShow () [position];
            attendee.AttendeeTypeIsSet = true;
            attendee.AttendeeType = NcAttendeeType.Optional;
            UpdateRequiredOptional ();
        }

        private void UpdateRequiredOptional ()
        {
            required = allAttendees.Where (x => x.AttendeeType == NcAttendeeType.Required).ToList ();
            optional = allAttendees.Where (x => x.AttendeeType != NcAttendeeType.Required).ToList ();
            NotifyDataSetChanged ();
            if (null != changedCallback) {
                changedCallback ();
            }
        }

        private List<McAttendee> CollectionToShow ()
        {
            switch (state) {
            case AttendeeListBaseFragment.CurrentTab.All:
                return allAttendees;
            case AttendeeListBaseFragment.CurrentTab.Required:
                return required;
            case AttendeeListBaseFragment.CurrentTab.Optional:
                return optional;
            default:
                NcAssert.CaseError (string.Format ("Illegal value of AttendeeListAdapter.State: {0} ({1})", state.ToString (), (int)state));
                return new List<McAttendee> ();
            }
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override int Count {
            get {
                return CollectionToShow ().Count;
            }
        }

        public override McAttendee this[int index] {
            get {
                return CollectionToShow () [index];
            }
        }

        public override int ViewTypeCount {
            get {
                return AttendeeListBaseFragment.NUM_CELL_TYPES;
            }
        }

        public override int GetItemViewType (int position)
        {
            if (NcAttendeeType.Required == this[position].AttendeeType) {
                return AttendeeListBaseFragment.REQUIRED_CELL_TYPE;
            }
            return AttendeeListBaseFragment.OPTIONAL_CELL_TYPE;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View cell = convertView;
            if (null == cell) {
                cell = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.AttendeeListCell, parent, false);
            }
            var attendee = this [position];
            var initialsView = cell.FindViewById<ContactPhotoView> (Resource.Id.user_image);
            var nameView = cell.FindViewById<TextView> (Resource.Id.attendee_name);
            var emailView = cell.FindViewById<TextView> (Resource.Id.attendee_email);
            var initials = ContactsHelper.NameToLetters (attendee.DisplayName);
            var color = Util.ColorResourceForEmail (attendee.AccountId, attendee.Email);
            initialsView.SetEmailAddress (attendee.AccountId, attendee.Email, initials, color);
            nameView.Text = attendee.DisplayName;
            emailView.Text = attendee.Email;
            return cell;
        }
    }
}

