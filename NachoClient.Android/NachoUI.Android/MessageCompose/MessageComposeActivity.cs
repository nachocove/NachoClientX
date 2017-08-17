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
using Android.Support.V7.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{

    [Activity (WindowSoftInputMode = Android.Views.SoftInput.AdjustResize)]
    public class MessageComposeActivity : NcActivity
    {

        private MessageComposer Composer;
        private MessageComposeFragment ComposeFragment;
        private bool _IsSending;
        private bool IsSending {
            get {
                return _IsSending;
            }
            set {
                _IsSending = value;
                InvalidateOptionsMenu ();
            }
        }

        #region Intents

        public const string EXTRA_ACTION = "com.nachocove.nachomail.action";
        public const string EXTRA_ACCOUNT_ID = "com.nachocove.nachomail.accountId";
        public const string EXTRA_RELATED_MESSAGE_ID = "com.nachocove.nachomail.relatedMessageId";
        public const string EXTRA_RELATED_CALENDAR_ID = "com.nachocove.nachomail.relatedCalendarId";
        public const string EXTRA_MESSAGE = "com.nachocove.nachomail.message";
        public const string EXTRA_MESSAGE_ID = "com.nachocove.nachomail.messageId";
        public const string EXTRA_INITIAL_TEXT = "com.nachocove.nachomail.initialText";
        public const string EXTRA_INITIAL_RECIPIENT = "com.nachocove.nachomail.initialRecipient";
        public const string EXTRA_INITIAL_QUICK_REPLY = "com.nachocove.nachomail.initialQuickReply";
        public const string EXTRA_INITIAL_ATTACHMENT = "com.nachocove.nachomail.initialAttachment";
        public const string EXTRA_INITIAL_ATTACHMENTS = "com.nachocove.nachomail.initialAttachments";

        public static Intent NewMessageIntent (Context context, int accountId, string recipient = null)
        {
            var intent = new Intent (context, typeof (MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            if (!String.IsNullOrEmpty (recipient)) {
                intent.PutExtra (EXTRA_INITIAL_RECIPIENT, recipient);
            }
            intent.PutExtra (EXTRA_ACCOUNT_ID, accountId);
            return intent;
        }

        public static Intent ForwardAttachmentIntent (Context context, int accountId, int attachmentId)
        {
            var intent = new Intent (context, typeof (MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_INITIAL_ATTACHMENT, attachmentId);
            intent.PutExtra (EXTRA_ACCOUNT_ID, accountId);
            return intent;
        }

        public static Intent RespondIntent (Context context, EmailHelper.Action action, McEmailMessage relatedMessage, bool quickReply = false)
        {
            var intent = new Intent (context, typeof (MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_ACTION, (int)action);
            intent.PutExtra (EXTRA_RELATED_MESSAGE_ID, relatedMessage.Id);
            intent.PutExtra (EXTRA_ACCOUNT_ID, relatedMessage.AccountId);
            intent.PutExtra (EXTRA_INITIAL_QUICK_REPLY, quickReply);
            return intent;
        }

        public static Intent InitialTextIntent (Context context, McEmailMessage message, string text)
        {
            var intent = new Intent (context, typeof (MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_MESSAGE, IntentHelper.StoreValue (message));
            intent.PutExtra (EXTRA_ACCOUNT_ID, message.AccountId);
            intent.PutExtra (EXTRA_INITIAL_TEXT, text);
            return intent;
        }

        public static Intent MessageWithAttachmentsIntent (Context context, McEmailMessage message, string text, IList<McAttachment> attachments)
        {
            var intent = new Intent (context, typeof (MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_MESSAGE, IntentHelper.StoreValue (message));
            intent.PutExtra (EXTRA_ACCOUNT_ID, message.AccountId);
            intent.PutExtra (EXTRA_INITIAL_TEXT, text);
            int [] attachmentIds = new int [attachments.Count];
            int a = 0;
            foreach (var attachment in attachments) {
                attachmentIds [a++] = attachment.Id;
            }
            intent.PutExtra (EXTRA_INITIAL_ATTACHMENTS, attachmentIds);
            return intent;
        }

        public static Intent ForwardCalendarIntent (Context context, int calendarId, McEmailMessage message)
        {
            var intent = new Intent (context, typeof (MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_ACTION, (int)EmailHelper.Action.Forward);
            intent.PutExtra (EXTRA_RELATED_CALENDAR_ID, calendarId);
            intent.PutExtra (EXTRA_MESSAGE, IntentHelper.StoreValue (message));
            intent.PutExtra (EXTRA_ACCOUNT_ID, message.AccountId);
            return intent;
        }

        public static Intent DraftIntent (Context context, McEmailMessage message)
        {
            var intent = new Intent (context, typeof (MessageComposeActivity));
            intent.SetAction (Intent.ActionSend);
            intent.PutExtra (EXTRA_MESSAGE, IntentHelper.StoreValue (message));
            intent.PutExtra (EXTRA_ACCOUNT_ID, message.AccountId);
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;

        void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
        }

        void ClearSubviews ()
        {
            Toolbar = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle bundle)
        {
            Log.Info (Log.LOG_UI, "MessageComposeActivity OnCreate");
            base.OnCreate (bundle);
            SetContentView (Resource.Layout.MessageComposeActivity);
            FindSubviews ();
            if (ComposeFragment.Composer != null) {
                Composer = ComposeFragment.Composer;
            } else if (bundle != null) {
                PopulateFromSavedBundle (bundle);
            } else {
                PopulateFromIntent ();
            }
            Toolbar.Title = "";
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
        }

        public override void OnAttachFragment (Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is MessageComposeFragment) {
                ComposeFragment = (fragment as MessageComposeFragment);
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            Log.Info (Log.LOG_UI, "MessageComposeActivity OnSaveInstanceState");
            base.OnSaveInstanceState (outState);
            outState.PutInt (EXTRA_MESSAGE_ID, Composer.Message.Id);
            ComposeFragment.Save (() => {
                Log.Info (Log.LOG_UI, "MessageComposeActivity OnSaveInstanceState...Save done");
            });
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        void PopulateFromSavedBundle (Bundle bundle)
        {
            var messageId = bundle.GetInt (EXTRA_MESSAGE_ID);
            var message = McEmailMessage.QueryById<McEmailMessage> (messageId);
            var accountId = message.AccountId;
            Composer = new MessageComposer (accountId);
            Composer.Message = message;
            ComposeFragment.Composer = Composer;
        }

        void PopulateFromIntent ()
        {
            NcAssert.True (Intent.HasExtra (EXTRA_ACCOUNT_ID));
            var accountId = Intent.Extras.GetInt (EXTRA_ACCOUNT_ID);
            Composer = new MessageComposer (accountId);

            if (Intent.HasExtra (EXTRA_ACTION)) {
                Composer.Kind = (NachoCore.Utils.EmailHelper.Action)Intent.GetIntExtra (EXTRA_ACTION, 0);
            }
            if (Intent.HasExtra (EXTRA_RELATED_CALENDAR_ID)) {
                var relatedCalendarItem = McCalendar.QueryById<McCalendar> (Intent.GetIntExtra (EXTRA_RELATED_CALENDAR_ID, 0));
                Composer.RelatedCalendarItem = relatedCalendarItem;
            }
            Log.Info (Log.LOG_UI, "MessageComposeActivity OnCreate...RetainedData == null");
            if (Intent.HasExtra (EXTRA_RELATED_MESSAGE_ID)) {
                var relatedThread = new McEmailMessageThread ();
                relatedThread.FirstMessageId = Intent.GetIntExtra (EXTRA_RELATED_MESSAGE_ID, 0);
                relatedThread.MessageCount = 1;
                Composer.RelatedThread = relatedThread;
            }
            if (Intent.HasExtra (EXTRA_MESSAGE)) {
                var message = IntentHelper.RetrieveValue<McEmailMessage> (Intent.GetStringExtra (EXTRA_MESSAGE));
                Composer.Message = message;
            }
            if (Intent.HasExtra (EXTRA_INITIAL_TEXT)) {
                var text = Intent.GetStringExtra (EXTRA_INITIAL_TEXT);
                Composer.InitialText = text;
            }
            if (Intent.HasExtra (EXTRA_INITIAL_RECIPIENT)) {
                var to = Intent.GetStringExtra (EXTRA_INITIAL_RECIPIENT);
                Composer.InitialRecipient = to;
            }
            if (Intent.HasExtra (EXTRA_INITIAL_QUICK_REPLY)) {
                Composer.InitialQuickReply = Intent.GetBooleanExtra (EXTRA_INITIAL_QUICK_REPLY, false);
            }
            if (Intent.HasExtra (EXTRA_INITIAL_ATTACHMENT)) {
                var attachmentId = Intent.GetIntExtra (EXTRA_INITIAL_ATTACHMENT, 0);
                if (0 != attachmentId) {
                    var attachment = McAttachment.QueryById<McAttachment> (attachmentId);
                    if (null != attachment) {
                        Composer.InitialAttachments.Add (attachment);
                    }
                }
            }
            if (Intent.HasExtra (EXTRA_INITIAL_ATTACHMENTS)) {
                var attachmentIds = Intent.GetIntArrayExtra (EXTRA_INITIAL_ATTACHMENTS);
                foreach (int id in attachmentIds) {
                    var attachment = McAttachment.QueryById<McAttachment> (id);
                    if (null != attachment) {
                        Composer.InitialAttachments.Add (attachment);
                    }
                }
            }
            ComposeFragment.Composer = Composer;
        }

        #endregion

        #region Options Menu

        public override bool OnCreateOptionsMenu (Android.Views.IMenu menu)
        {
            MenuInflater.Inflate (Resource.Menu.message_compose, menu);
            var sendItem = menu.FindItem (Resource.Id.send);
            bool sendEnabled = ComposeFragment.CanSend && !IsSending;
            sendItem.SetEnabled (sendEnabled);
            if (!sendEnabled) {
                var dimmedIcon = sendItem.Icon.Mutate ();
                dimmedIcon.SetAlpha (85);
                sendItem.SetIcon (dimmedIcon);
            }
            return base.OnCreateOptionsMenu (menu);
        }

        public override bool OnOptionsItemSelected (Android.Views.IMenuItem item)
        {
            switch (item.ItemId) {
            case Android.Resource.Id.Home:
                FinishWithSaveConfirmation ();
                return true;
            case Resource.Id.send:
                Send ();
                return true;
            case Resource.Id.attach:
                PickAttachment ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        public override void OnBackPressed ()
        {
            FinishWithSaveConfirmation ();
        }

        #endregion

        #region User Actions

        void Send ()
        {
            IsSending = true;
            ComposeFragment.Send ((bool sent) => {
                if (sent) {
                    Finish ();
                } else {
                    IsSending = false;
                }
            });
        }

        void PickAttachment ()
        {
            ComposeFragment.PickAttachment ();
        }

        #endregion

        #region Draft Management

        private void FinishWithSaveConfirmation ()
        {
            ComposeFragment.EndEditing ();
            var alert = new Android.App.AlertDialog.Builder (this);
            alert.SetItems (new string []{
                GetString (Resource.String.message_compose_close_save),
                GetString (Resource.String.message_compose_close_discard),
            }, (sender, e) => {
                switch (e.Which) {
                case 0:
                    Save ();
                    break;
                case 1:
                    Discard ();
                    break;
                }
            });
            alert.Show ();
        }

        public void Discard ()
        {
            Composer.Message.Delete ();
            Finish ();
        }

        public void Save ()
        {
            ComposeFragment.Save (() => {
                Finish ();
            });
        }

        #endregion
    }
}

