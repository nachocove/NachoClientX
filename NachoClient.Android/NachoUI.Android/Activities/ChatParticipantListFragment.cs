//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System.Collections.Generic;
using NachoCore.Model;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class ChatParticipantListFragment : Fragment
    {
        public int accountId;
        public IList<McChatParticipant> participants;
        protected ParticipantListAdapter adapter;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ChatParticipantListFragment, container, false);

            return view;
        }

        public override void OnActivityCreated (Bundle savedInstanceState)
        {
            base.OnActivityCreated (savedInstanceState);

            adapter = new ParticipantListAdapter ();
            adapter.participants = participants;
            adapter.accountId = accountId;

            var listView = View.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.Adapter = adapter;
            listView.setMenuCreator (SwipeMenu_Create);
            listView.setOnMenuItemClickListener (SwipeMenu_Click);
            listView.ItemClick += ListView_ItemClick;
        }

        private void SwipeMenu_Create (SwipeMenu menu)
        {
        }

        private bool SwipeMenu_Click (int position, SwipeMenu menu, int index)
        {
            return false;
        }

        void ListView_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            var participant = participants [e.Position];
            var contact = McContact.QueryById<McContact> (participant.ContactId);
            if (null != contact) {
                Activity.StartActivity (ContactViewActivity.ShowContactIntent (Activity, contact));
            }
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }
    }

    public class ParticipantListAdapter : BaseAdapter<McChatParticipant>
    {
        public int accountId;
        public IList<McChatParticipant> participants;

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
            var nameView = cell.FindViewById<TextView> (Resource.Id.participant_name);
            var emailView = cell.FindViewById<TextView> (Resource.Id.participant_email);
            var initials = ContactsHelper.NameToLetters (participant.CachedName);
            var color = Util.ColorResourceForEmail (accountId, participant.EmailAddress);
            initialsView.SetEmailAddress (accountId, participant.EmailAddress, initials, color);
            nameView.Text = participant.CachedName;
            emailView.Text = participant.EmailAddress;
            return cell;
        }
    }
}

