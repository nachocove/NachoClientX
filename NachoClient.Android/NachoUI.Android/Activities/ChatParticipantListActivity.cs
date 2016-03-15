//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System.Collections.Generic;
using NachoCore.Model;
using Android.OS;
using Android.Content;
using System;
using Android.App;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "ChatParticipantListActivity")]
    public class ChatParticipantListActivity : NcActivityWithData<IList<McChatParticipant>>
    {
        private const string EXTRA_ACCOUNT = "com.nachocove.nachomail.EXTRA_ACCOUNT";
        private const string EXTRA_PARTICIPANTS = "com.nachocove.nachomail.EXTRA_PARTICIPANTS";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.ChatParticipantListActivity);

            var fragment = FragmentManager.FindFragmentById<ChatParticipantListFragment> (Resource.Id.chat_participant_list_fragment);
            fragment.accountId = AccountIdFromIntent (Intent);
            var participants = RetainedData;
            if (null == participants) {
                participants = ParticipantsFromIntent (Intent);
                RetainedData = participants;
            }
            fragment.participants = participants;
        }

        public static Intent ParticipantsIntent (Context context, Type activityType, string action, int accountId, IList<McChatParticipant> participants)
        {
            var intent = new Intent (context, activityType);
            intent.SetAction (action);
            intent.PutExtra (EXTRA_ACCOUNT, accountId);
            intent.PutExtra (EXTRA_PARTICIPANTS, IntentHelper.StoreValue (participants));
            return intent;
        }

        public static int AccountIdFromIntent (Intent intent)
        {
            return intent.GetIntExtra (EXTRA_ACCOUNT, 0);
        }

        public static IList<McChatParticipant> ParticipantsFromIntent (Intent intent)
        {
            return IntentHelper.RetrieveValue<IList<McChatParticipant>> (intent.GetStringExtra (EXTRA_PARTICIPANTS));
        }
    }
}

