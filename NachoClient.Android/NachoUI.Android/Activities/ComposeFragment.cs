
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

        public static ComposeFragment newInstance ()
        {
            var fragment = new ComposeFragment ();
            return fragment;
        }

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
            messageToSend = messageToSend.UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.BodyPreview = preview;
                target.Intent = McEmailMessage.IntentType.None;
                target.IntentDate = default(DateTime);
                target.IntentDateType = MessageDeferralType.None;
                target.QRType = NachoCore.Brain.NcQuickResponse.QRTypeEnum.None;
                if (EmailHelper.IsForwardOrReplyAction (action)) {
                    target.ReferencedEmailId = referencedMessage.Id;
                    target.ReferencedBodyIsIncluded = false;
                    target.ReferencedIsForward = EmailHelper.IsForwardAction (action);
                    target.WaitingForAttachmentsToDownload = false;
                }
                return true;
            });

            // Send the mesage
            EmailHelper.SendTheMessage (action, messageToSend, false, referencedMessage, false, null);

            return true;
        }
    }
}

