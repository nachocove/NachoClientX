using System;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class MessageComposeActivityData
    {
        public int MessageId;
        public bool MessageSaved;
        public List<Action> MessageSavedEvents = new List<Action> ();

        public void FireEvent ()
        {
            foreach (var action in MessageSavedEvents) {
                action ();
            }
            MessageSavedEvents.Clear ();
        }
    }

    [Activity (Label = "MessageComposeActivity")]            
    public class MessageComposeActivity : NcActivityWithData<MessageComposeActivityData>
    {
        public const string EXTRA_ACTION = "com.nachocove.nachomail.action";
        public const string EXTRA_ACCOUNT_ID = "com.nachocove.nachomail.accountId";
        public const string EXTRA_RELATED_MESSAGE_ID = "com.nachocove.nachomail.relatedMessageId";
        public const string EXTRA_RELATED_CALENDAR_ID = "com.nachocove.nachomail.relatedCalendarId";
        public const string EXTRA_MESSAGE = "com.nachocove.nachomail.message";
        public const string EXTRA_INITIAL_TEXT = "com.nachocove.nachomail.initialText";
        public const string EXTRA_INITIAL_RECIPIENT = "com.nachocove.nachomail.initialRecipient";
        public const string EXTRA_INITIAL_QUICK_REPLY = "com.nachocove.nachomail.initialQuickReply";
        public const string EXTRA_INITIAL_ATTACHMENT = "com.nachocove.nachomail.initialAttachment";
        public const string EXTRA_INITIAL_ATTACHMENTS = "com.nachocove.nachomail.initialAttachments";

        private const string COMPOSE_FRAGMENT_TAG = "ComposeFragment";

        private ComposeFragment composeFragment;
        private MessageComposeActivityData savedMessageInfo;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.MessageComposeActivity);

            composeFragment = null;
            if (null != bundle) {
                composeFragment = FragmentManager.FindFragmentByTag<ComposeFragment> (COMPOSE_FRAGMENT_TAG);
            }
            if (null == composeFragment) {
                NcAssert.True (Intent.HasExtra (EXTRA_ACCOUNT_ID));
                var account = McAccount.QueryById<McAccount> (Intent.GetIntExtra (EXTRA_ACCOUNT_ID, 0));
                composeFragment = new ComposeFragment (account);
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, composeFragment, COMPOSE_FRAGMENT_TAG).Commit ();
            }

            if (Intent.HasExtra (EXTRA_ACTION)) {
                composeFragment.Composer.Kind = (NachoCore.Utils.EmailHelper.Action)Intent.GetIntExtra (EXTRA_ACTION, 0);
            }
            if (Intent.HasExtra (EXTRA_RELATED_CALENDAR_ID)) {
                var relatedCalendarItem = McCalendar.QueryById<McCalendar> (Intent.GetIntExtra (EXTRA_RELATED_CALENDAR_ID, 0));
                composeFragment.Composer.RelatedCalendarItem = relatedCalendarItem;
            }

            if (null != RetainedData) {
                savedMessageInfo = RetainedData;
                composeFragment.Composer.Message = McEmailMessage.QueryById<McEmailMessage> (savedMessageInfo.MessageId);
                if (!savedMessageInfo.MessageSaved) {
                    composeFragment.MessageIsReady = false;
                    savedMessageInfo.MessageSavedEvents.Add (() => {
                        composeFragment.MessageIsReady = true;
                    });
                }
            } else {
                if (Intent.HasExtra (EXTRA_RELATED_MESSAGE_ID)) {
                    var relatedThread = new McEmailMessageThread ();
                    relatedThread.FirstMessageId = Intent.GetIntExtra (EXTRA_RELATED_MESSAGE_ID, 0);
                    relatedThread.MessageCount = 1;
                    composeFragment.Composer.RelatedThread = relatedThread;
                }
                if (Intent.HasExtra (EXTRA_MESSAGE)) {
                    var message = IntentHelper.RetrieveValue<McEmailMessage> (Intent.GetStringExtra (EXTRA_MESSAGE));
                    composeFragment.Composer.Message = message;
                }
                if (Intent.HasExtra (EXTRA_INITIAL_TEXT)) {
                    var text = Intent.GetStringExtra (EXTRA_INITIAL_TEXT);
                    composeFragment.Composer.InitialText = text;
                }
                if (Intent.HasExtra (EXTRA_INITIAL_RECIPIENT)) {
                    var to = Intent.GetStringExtra (EXTRA_INITIAL_RECIPIENT);
                    composeFragment.Composer.InitialRecipient = to;
                }
                if (Intent.HasExtra (EXTRA_INITIAL_QUICK_REPLY)) {
                    composeFragment.Composer.InitialQuickReply = Intent.GetBooleanExtra (EXTRA_INITIAL_QUICK_REPLY, false);
                }
                if (Intent.HasExtra (EXTRA_INITIAL_ATTACHMENT)) {
                    var attachmentId = Intent.GetIntExtra (EXTRA_INITIAL_ATTACHMENT, 0);
                    if (0 != attachmentId) {
                        var attachment = McAttachment.QueryById<McAttachment> (attachmentId);
                        if (null != attachment) {
                            composeFragment.Composer.InitialAttachments.Add (attachment);
                        }
                    }
                }
                if (Intent.HasExtra (EXTRA_INITIAL_ATTACHMENTS)) {
                    var attachmentIds = Intent.GetIntArrayExtra (EXTRA_INITIAL_ATTACHMENTS);
                    foreach (int id in attachmentIds) {
                        var attachment = McAttachment.QueryById<McAttachment> (id);
                        if (null != attachment) {
                            composeFragment.Composer.InitialAttachments.Add (attachment);
                        }
                    }
                }
                savedMessageInfo = new MessageComposeActivityData ();
                RetainedData = savedMessageInfo;
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            savedMessageInfo.MessageId = composeFragment.Composer.Message.Id;
            savedMessageInfo.MessageSaved = false;
            composeFragment.Save (() => {
                savedMessageInfo.MessageSaved = true;
                savedMessageInfo.FireEvent ();
            });
        }

        public static Intent NewMessageIntent (Context context, int accountId, string recipient = null)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            if (!String.IsNullOrEmpty (recipient)) {
                intent.PutExtra (EXTRA_INITIAL_RECIPIENT, recipient);
            }
            intent.PutExtra (EXTRA_ACCOUNT_ID, accountId);
            return intent;
        }

        public static Intent ForwardAttachmentIntent (Context context, int accountId, int attachmentId)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_INITIAL_ATTACHMENT, attachmentId);
            intent.PutExtra (EXTRA_ACCOUNT_ID, accountId);
            return intent;
        }

        public static Intent RespondIntent (Context context, EmailHelper.Action action, McEmailMessage relatedMessage, bool quickReply = false)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_ACTION, (int)action);
            intent.PutExtra (EXTRA_RELATED_MESSAGE_ID, relatedMessage.Id);
            intent.PutExtra (EXTRA_ACCOUNT_ID, relatedMessage.AccountId);
            intent.PutExtra (EXTRA_INITIAL_QUICK_REPLY, quickReply);
            return intent;
        }

        public static Intent InitialTextIntent (Context context, McEmailMessage message, string text)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_MESSAGE, IntentHelper.StoreValue (message));
            intent.PutExtra (EXTRA_ACCOUNT_ID, message.AccountId);
            intent.PutExtra (EXTRA_INITIAL_TEXT, text);
            return intent;
        }

        public static Intent MessageWithAttachmentsIntent (Context context, McEmailMessage message, string text, IList<McAttachment> attachments)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_MESSAGE, IntentHelper.StoreValue (message));
            intent.PutExtra (EXTRA_ACCOUNT_ID, message.AccountId);
            intent.PutExtra (EXTRA_INITIAL_TEXT, text);
            int[] attachmentIds = new int[attachments.Count];
            int a = 0;
            foreach (var attachment in attachments) {
                attachmentIds [a++] = attachment.Id;
            }
            intent.PutExtra (EXTRA_INITIAL_ATTACHMENTS, attachmentIds);
            return intent;
        }

        public static Intent ForwardCalendarIntent (Context context, int calendarId, McEmailMessage message)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_ACTION, (int)EmailHelper.Action.Forward);
            intent.PutExtra (EXTRA_RELATED_CALENDAR_ID, calendarId);
            intent.PutExtra (EXTRA_MESSAGE, IntentHelper.StoreValue (message));
            intent.PutExtra (EXTRA_ACCOUNT_ID, message.AccountId);
            return intent;
        }

        public static Intent DraftIntent (Context context, McEmailMessage message)
        {
            var intent = new Intent (context, typeof(MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_MESSAGE, IntentHelper.StoreValue (message));
            intent.PutExtra (EXTRA_ACCOUNT_ID, message.AccountId);
            return intent;
        }

        public override void OnBackPressed ()
        {
            var alert = new Android.App.AlertDialog.Builder (this).SetTitle ("Would you like to save this message?").SetMessage ("You can access saved messages from your Drafts folder.");
            alert.SetNegativeButton ("Discard Draft", Discard);
            alert.SetPositiveButton ("Save Draft", Save);
            alert.Show ();
        }

        public void Discard (object sender, EventArgs args)
        {
            var fragment = FragmentManager.FindFragmentById<ComposeFragment> (Resource.Id.content);
            fragment.Discard ();
            Finish ();
        }

        public void Save (object sender, EventArgs args)
        {
            var fragment = FragmentManager.FindFragmentById<ComposeFragment> (Resource.Id.content);
            fragment.Save (() => {
                Finish ();
            });
        }
    }
}

