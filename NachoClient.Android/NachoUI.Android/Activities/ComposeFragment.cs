
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

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

using MimeKit;

namespace NachoClient.AndroidClient
{
    public class ComposeFragment : Fragment
    {
        McAccount account;

        EmailHelper.Action action;
        McEmailMessage referencedMessage;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            action = EmailHelper.Action.Send;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.ComposeFragment, container, false);

            var sendButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            sendButton.SetImageResource (Resource.Drawable.icn_send);
            sendButton.Visibility = Android.Views.ViewStates.Visible;
            sendButton.Click += SendButton_Click;

            account = NcApplication.Instance.Account;

            return view;
        }

        void SendButton_Click (object sender, EventArgs e)
        {
            if (SendMessage ()) {
                this.Activity.Finish ();
            }
        }

        bool SendMessage ()
        {
            var toView = View.FindViewById<EditText> (Resource.Id.to_list);
            var toList = NcEmailAddress.ParseToAddressListString (toView.Text);

            var ccView = View.FindViewById<EditText> (Resource.Id.cc_list);
            var ccList = NcEmailAddress.ParseCcAddressListString (ccView.Text);

            // FIXME: bcc
            var bccList = NcEmailAddress.ParseBccAddressListString ("");

            var mimeMessage = EmailHelper.CreateMessage (account, toList, ccList, bccList);

            var subjectView = View.FindViewById<EditText> (Resource.Id.subject);
            mimeMessage.Subject = EmailHelper.CreateSubjectWithIntent (subjectView.Text, McEmailMessage.IntentType.None, MessageDeferralType.None, default(DateTime));

            EmailHelper.SetupReferences (ref mimeMessage, referencedMessage);

            var messageView = View.FindViewById<EditText> (Resource.Id.message);

            var body = new BodyBuilder ();
            body.TextBody = messageView.Text;

            var length = Math.Min (messageView.Text.Length, 256);
            var preview = messageView.Text.Substring (0, length);

            mimeMessage.Body = body.ToMessageBody ();
            var messageToSend = MimeHelpers.AddToDb (account.Id, mimeMessage);
            messageToSend.BodyPreview = preview;
            messageToSend.Intent = McEmailMessage.IntentType.None;
            messageToSend.IntentDate = default(DateTime);
            messageToSend.IntentDateType = MessageDeferralType.None;
            messageToSend.QRType = NachoCore.Brain.NcQuickResponse.QRTypeEnum.None;

            if (EmailHelper.IsForwardOrReplyAction (action)) {
                messageToSend.ReferencedEmailId = referencedMessage.Id;
                messageToSend.ReferencedBodyIsIncluded = false;
                messageToSend.ReferencedIsForward = EmailHelper.IsForwardAction (action);
                messageToSend.WaitingForAttachmentsToDownload = false;
            }

            messageToSend.Update ();

            // Send the mesage
            EmailHelper.SendTheMessage (action, messageToSend, false, referencedMessage, false, null);

            return true;
        }
    }
}

