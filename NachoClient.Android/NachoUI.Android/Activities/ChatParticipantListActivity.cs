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
using Android.Views;
using Android.Widget;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class CharParticipantListActivity : NcActivityWithData<IList<McChatParticipant>>
    {
        private const string EXTRA_ACCOUNT = "com.nachocove.nachomail.EXTRA_ACCOUNT";
        private const string EXTRA_PARTICIPANTS = "com.nachocove.nachomail.EXTRA_PARTICIPANTS";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.ChatParticipantListActivity);

            var fragment = FragmentManager.FindFragmentById<ChatParticipantListFragment> (Resource.Id.chat_participant_list_fragment);
            fragment.AccountId = AccountIdFromIntent (Intent);
            var participants = RetainedData;
            if (null == participants) {
                participants = ParticipantsFromIntent (Intent);
                RetainedData = participants;
            }
            fragment.Participants = participants;
        }

        public override void OnBackPressed ()
        {
            var fragment = FragmentManager.FindFragmentById<ChatParticipantListFragment> (Resource.Id.chat_participant_list_fragment);
            SetResult (Result.Ok, ResultIntent (fragment.Participants));
            Finish ();
        }


        protected static Intent ParticipantsIntent (Context context, Type activityType, string action, int accountId, IList<McChatParticipant> participants)
        {
            var intent = new Intent (context, activityType);
            intent.SetAction (action);
            intent.PutExtra (EXTRA_ACCOUNT, accountId);
            intent.PutExtra (EXTRA_PARTICIPANTS, IntentHelper.StoreValue (participants));
            return intent;
        }

        public static Intent ResultIntent (IList<McChatParticipant> participants)
        {
            var intent = new Intent ();
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

