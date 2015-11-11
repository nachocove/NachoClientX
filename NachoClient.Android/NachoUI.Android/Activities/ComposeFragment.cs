﻿
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
using NachoCore.Brain;

using MimeKit;
using Java.Interop;
using NachoPlatform;
using System.IO;

namespace NachoClient.AndroidClient
{
    public class ComposeFragment : 
        Fragment,
        NachoJavascriptMessageHandler,
        MessageComposerDelegate,
        NachoWebClientDelegate,
        MessageComposeHeaderViewDelegate,
        IntentFragmentDelegate,
        AttachmentPickerFragmentDelegate,
        QuickResponseFragmentDelegate
    {

        #region Properties

        public readonly MessageComposer Composer;
        MessageComposeHeaderView HeaderView;
        Android.Webkit.WebView WebView;
        Android.Widget.ImageView SendButton;
        Android.Widget.ImageView QuickResponseButton;
        bool IsWebViewLoaded;
        bool FocusWebViewOnLoad;
        List<Tuple<string, JavascriptCallback>> JavaScriptQueue;
        ButtonBar buttonBar;

        #endregion

        #region Constructor/Factory

        public ComposeFragment () : base()
        {
            Composer = new MessageComposer (NcApplication.Instance.Account);
            Composer.Delegate = this;
            JavaScriptQueue = new List<Tuple<string, JavascriptCallback>> ();
        }

        public static ComposeFragment newInstance ()
        {
            var fragment = new ComposeFragment ();
            return fragment;
        }

        #endregion

        #region Lifecycle

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.ComposeFragment, container, false);

            buttonBar = new ButtonBar (view);

            buttonBar.SetIconButton (ButtonBar.Button.Right1, Resource.Drawable.icn_send, SendButton_Click);
            buttonBar.SetIconButton (ButtonBar.Button.Right2, Resource.Drawable.contact_quickemail, QuickResponseButton_Click);
            SendButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            QuickResponseButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button2);

            HeaderView = view.FindViewById<MessageComposeHeaderView> (Resource.Id.header);
            HeaderView.Delegate = this;

            WebView = view.FindViewById<Android.Webkit.WebView> (Resource.Id.message);
            WebView.Settings.JavaScriptEnabled = true;
            WebView.AddJavascriptInterface (new NachoJavascriptMessenger(this, "nacho"), "_android_messageHandlers_nacho");
            WebView.AddJavascriptInterface (new NachoJavascriptMessenger(this, "nachoCompose"), "_android_messageHandlers_nachoCompose");
            WebView.SetWebViewClient (new NachoWebClient (this));

            Composer.StartPreparingMessage ();

            UpdateHeader ();
            UpdateSendEnabled ();

            return view;
        }

        #endregion

        #region User Actions - Navbar

        void SendButton_Click (object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace (Composer.Message.Subject)) {
                var alert = new AlertDialog.Builder (Activity).SetTitle ("Empty Subject").SetMessage ("This message does not have a subject.  How would you like to proceed?");
                alert.SetNeutralButton ("Send Anyway", SendWithoutSubject);
                alert.SetPositiveButton ("Add Subject", AddSubject);
                alert.Show ();
            } else {
                CheckSizeBeforeSending ();
            }
        }

        void SendWithoutSubject (object sender, EventArgs args)
        {
            CheckSizeBeforeSending ();
        }

        void AddSubject (object sender, EventArgs args)
        {
            HeaderView.FocusSubject ();
        }

        void CheckSizeBeforeSending ()
        {
            SendButton.Enabled = false;
            GetHtmlContent ((string html) => {
                SendButton.Enabled = true;
                Composer.Save (html);
                if (Composer.IsOversize) {
                    if (Composer.CanResize) {
                        var alert = new AlertDialog.Builder (Activity).SetTitle (String.Format ("This message is {0}. Do you want to resize images?", Pretty.PrettyFileSize(Composer.MessageSize)));
                        alert.SetSingleChoiceItems (new string[] {
                            String.Format ("Small Images ({0})", Pretty.PrettyFileSize(Composer.EstimatedSmallSize)),
                            String.Format ("Medium Images ({0})", Pretty.PrettyFileSize(Composer.EstimatedMediumSize)),
                            String.Format ("Large Images ({0})", Pretty.PrettyFileSize(Composer.EstimatedLargeSize)),
                            String.Format ("Actual Size ({0})", Pretty.PrettyFileSize(Composer.MessageSize))
                        }, -1, (object sender, DialogClickEventArgs e) => {
                            switch (e.Which){
                            case 0:
                                ResizeImagesSmall (sender, e);
                                break;
                            case 1:
                                ResizeImagesMedium (sender, e);
                                break;
                            case 2:
                                ResizeImagesLarge (sender, e);
                                break;
                            case 3:
                                AcknowlegeSizeWarning (sender, e);
                                break;
                            default:
                                NcAssert.CaseError();
                                break;
                            }
                        });
                        alert.Show ();
                    } else {
                        var alert = new AlertDialog.Builder (Activity).SetTitle ("Large Message").SetMessage (String.Format ("This message is {0}", Pretty.PrettyFileSize(Composer.MessageSize)));
                        alert.SetNeutralButton ("Send Anyway", AcknowlegeSizeWarning);
                        alert.Show ();
                    }
                } else {
                    Send ();
                }
            });
        }

        void AcknowlegeSizeWarning (object sender, EventArgs args)
        {
            Send ();
        }

        void ResizeImagesLarge (object sender, EventArgs args)
        {
            ResizeImagesAndSend (Composer.LargeImageLengths);
        }

        void ResizeImagesMedium (object sender, EventArgs args)
        {
            ResizeImagesAndSend (Composer.MediumImageLengths);
        }

        void ResizeImagesSmall (object sender, EventArgs args)
        {
            ResizeImagesAndSend (Composer.SmallImageLengths);
        }

        void ResizeImagesAndSend (Tuple<float, float> lengths)
        {
            Composer.ImageLengths = lengths;
            Send ();
        }

        void Send ()
        {
            Composer.Send ();
            Activity.Finish ();
        }

        void QuickResponseButton_Click (object sender, EventArgs e)
        {
            ShowQuickResponses ();
        }

        #endregion

        #region User Action - Header

        public void MessageComposeHeaderViewDidChangeTo (MessageComposeHeaderView view, string to)
        {
            Composer.Message.To = to;
            UpdateSendEnabled ();
        }

        public void MessageComposeHeaderViewDidChangeCc (MessageComposeHeaderView view, string cc)
        {
            Composer.Message.Cc = cc;
            UpdateSendEnabled ();
        }

        public void MessageComposeHeaderViewDidChangeBcc (MessageComposeHeaderView view, string bcc)
        {
            Composer.Message.Bcc = bcc;
            UpdateSendEnabled ();
        }

        public void MessageComposeHeaderViewDidChangeSubject (MessageComposeHeaderView view, string subject)
        {
            Composer.Message.Subject = subject;
        }
            
        public void MessageComposeHeaderViewDidSelectIntentField (MessageComposeHeaderView view)
        {
            var intentFragment = new IntentFragment ();
            intentFragment.Delegate = this;
            intentFragment.Show (FragmentManager, "com.nachocove.nachomail.composeIntent");
        }

        public void IntentFragmentDidSelectIntent (NcMessageIntent.MessageIntent intent)
        {
            Composer.Message.Intent = intent.type;
            Composer.Message.IntentDateType = MessageDeferralType.None;
            Composer.Message.IntentDate = DateTime.MinValue;
            UpdateHeaderIntentView ();
            if (intent.dueDateAllowed) {
                var deferralFragment = new ChooseDeferralFragment ();
                deferralFragment.Show (FragmentManager, "com.nachocove.nachomail.deferral");
                deferralFragment.type = NcMessageDeferral.MessageDateType.Intent;
                deferralFragment.setOnDeferralSelected (IntentDateSelected);
            }
        }

        public void IntentDateSelected (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate)
        {
            Composer.Message.IntentDateType = request;
            Composer.Message.IntentDate = selectedDate;
            UpdateHeaderIntentView ();
        }

        public void MessageComposeHeaderViewDidSelectAddAttachment (MessageComposeHeaderView view)
        {
            var attachmentPicker = new AttachmentPickerFragment ();
            attachmentPicker.Account = Composer.Account;
            attachmentPicker.Delegate = this;
            attachmentPicker.Show (FragmentManager, "attachments");
        }

        public void AttachmentPickerDidPickAttachment (AttachmentPickerFragment picker, McAttachment attachment)
        {
            attachment.Link (Composer.Message);
            HeaderView.AttachmentsView.AddAttachment (attachment);
        }

        public void MessageComposeHeaderViewDidSelectAttachment (MessageComposeHeaderView view, McAttachment attachment)
        {
            // TODO: display attachment
        }

        public void MessageComposeHeaderViewDidRemoveAttachment (MessageComposeHeaderView view, McAttachment attachment)
        {
            attachment.Unlink (Composer.Message);
        }

        public void QuickResponseFragmentDidSelectResponse (QuickResponseFragment fragment, NcQuickResponse.QuickResponse response)
        {
            if (!EmailHelper.IsReplyAction(Composer.Kind) && !EmailHelper.IsForwardAction(Composer.Kind)) {
                Composer.Message.Subject = response.subject;
                UpdateHeaderSubjectView ();
            }
            if (Composer.IsMessagePrepared) {
                var javascriptString = JavaScriptEscapedString (response.body + Composer.SignatureText());
                EvaluateJavascript (String.Format ("Editor.defaultEditor.replaceUserText({0});", javascriptString));
            } else {
                Composer.InitialText = response.body;
            }
            if (response.intent != null) {
                Composer.Message.Intent = response.intent.type;
            } else {
                Composer.Message.Intent = McEmailMessage.IntentType.None;
            }
            Composer.Message.IntentDate = DateTime.MinValue;
            Composer.Message.IntentDateType = MessageDeferralType.None;
            UpdateHeaderIntentView ();
            HeaderView.ShowIntentField ();
        }

        #endregion

        #region Web View

        void EvaluateJavascript (string js, JavascriptCallback callback = null)
        {
            if (IsWebViewLoaded) {
                JavascriptResultHandler handler = null;
                if (callback != null) {
                    handler = new JavascriptResultHandler (callback);
                }
                WebView.EvaluateJavascript (js, handler);
            } else {
                JavaScriptQueue.Add (new Tuple<string, JavascriptCallback> (js, callback));
            }
        }

        public void HandleJavascriptMessage (NachoJavascriptMessage message)
        {
        }

        public void OnPageFinished (Android.Webkit.WebView view, string url)
        {
            IsWebViewLoaded = true;
            EnableEditingInWebView ();
            foreach (var args in JavaScriptQueue) {
                EvaluateJavascript (args.Item1, args.Item2);
            }
            JavaScriptQueue = null;
            if (FocusWebViewOnLoad) {
                FocusWebView ();
            }
        }

        private void EnableEditingInWebView ()
        {
            EvaluateJavascript ("Editor.Enable()");
        }

        private void FocusWebView ()
        {
            EvaluateJavascript ("Editor.defaultEditor.focus()");
        }

        #endregion

        #region Message Composer

        public void MessageComposerDidCompletePreparation (MessageComposer composer)
        {
            DisplayMessageBody ();
        }

        public void MessageComposerDidFailToLoadMessage (MessageComposer composer)
        {
            NcAlertView.ShowMessage(Activity, "Could not load message", "Sorry, we could not load your message.  Please try again.");
        }

        public PlatformImage ImageForMessageComposerAttachment (MessageComposer composer, Stream stream)
        {
            return ImageAndroid.FromStream (stream);
        }

        void DisplayMessageBody ()
        {
            if (Composer.Bundle != null) {
                if (Composer.Bundle.FullHtmlUrl != null) {
                    WebView.LoadUrl (Composer.Bundle.FullHtmlUrl.AbsoluteUri);
                } else {
                    var html = Composer.Bundle.FullHtml;
                    WebView.LoadDataWithBaseURL (Composer.Bundle.BaseUrl.AbsoluteUri, html, "text/html", "utf-8", null);
                }
            }
        }

        #endregion

        #region Helpers

        string JavaScriptEscapedString (string s)
        {
            var primitive = new System.Json.JsonPrimitive (s);
            string escaped = "";
            using (var writer = new StringWriter ()) {
                primitive.Save (writer);
                escaped = writer.ToString ();
            }
            return escaped.Replace("\n", "\\n");
        }

        delegate void HtmlContentCallback (string html);

        void GetHtmlContent (HtmlContentCallback callback)
        {
            EvaluateJavascript ("document.documentElement.outerHTML", (Java.Lang.Object result) => {
                var stringResult = result as Java.Lang.String;
                // I don't know why the result is coming in as a JSON-encoded string value, but it is
                var json = stringResult.ToString ();
                var str = System.Json.JsonValue.Parse(json);
                callback ("<!DOCTYPE html>\n" + str);
            });
        }

        private void UpdateSendEnabled ()
        {
            SendButton.Enabled = Composer.HasRecipient;
        }

        private void UpdateHeader ()
        {
            HeaderView.ToField.AddressString = Composer.Message.To;
            HeaderView.CcField.AddressString = Composer.Message.Cc;
            HeaderView.BccField.AddressString = Composer.Message.Bcc;
            UpdateHeaderSubjectView ();
            UpdateHeaderIntentView ();
            UpdateHeaderAttachmentsView ();
        }

        void UpdateHeaderSubjectView ()
        {
            HeaderView.SubjectField.Text = Composer.Message.Subject;
        }

        void UpdateHeaderIntentView ()
        {
            HeaderView.IntentValueLabel.Text = NcMessageIntent.GetIntentString (Composer.Message.Intent, Composer.Message.IntentDateType, Composer.Message.IntentDate);
        }

        private void UpdateHeaderAttachmentsView ()
        {
            var attachments = McAttachment.QueryByItem (Composer.Message);
            HeaderView.AttachmentsView.SetAttachments (attachments);
        }

        void ShowQuickResponses ()
        {
            NcQuickResponse.QRTypeEnum responseType = NcQuickResponse.QRTypeEnum.Compose;

            if (EmailHelper.IsReplyAction (Composer.Kind)) {
                responseType = NcQuickResponse.QRTypeEnum.Reply;
            } else if (EmailHelper.IsForwardAction (Composer.Kind)) {
                responseType = NcQuickResponse.QRTypeEnum.Forward;
            }

            var quickResponsesFragment = new QuickResponseFragment (responseType);
            quickResponsesFragment.Delegate = this;
            quickResponsesFragment.Show (FragmentManager, "quick_responses");
        }

        #endregion

        #region Bride JS Result Interface to Delegate

        delegate void JavascriptCallback (Java.Lang.Object value);

        class JavascriptResultHandler : Java.Lang.Object, Android.Webkit.IValueCallback
        {

            JavascriptCallback Callback;

            public JavascriptResultHandler (JavascriptCallback callback) : base()
            {
                Callback = callback;
            }

            public void OnReceiveValue (Java.Lang.Object value)
            {
                Callback (value);
            }

        }

        #endregion

    }

    public interface NachoJavascriptMessageHandler {

        void HandleJavascriptMessage (NachoJavascriptMessage message);

    }

    public class NachoJavascriptMessage {

        object Body;
        string Name;

        public NachoJavascriptMessage (object body, string name)
        {
            Body = body;
            Name = name;
        }

    }

    public class NachoJavascriptMessenger : Java.Lang.Object
    {

        NachoJavascriptMessageHandler Handler;
        string Name;

        public NachoJavascriptMessenger (NachoJavascriptMessageHandler handler, string name)
        {
            Handler = handler;
            Name = name;
        }

        [Export ("postMessage")]
        [Android.Webkit.JavascriptInterface]
        void PostMessage (Java.Lang.Object o)
        {
            var message = new NachoJavascriptMessage (o, Name);
            Handler.HandleJavascriptMessage (message);
        }

    }

    interface NachoWebClientDelegate {

        void OnPageFinished  (Android.Webkit.WebView view, string url);

    }

    class NachoWebClient : Android.Webkit.WebViewClient 
    {

        NachoWebClientDelegate Delegate;

        public NachoWebClient (NachoWebClientDelegate del)
        {
            Delegate = del;
        }

        public override void OnPageFinished (Android.Webkit.WebView view, string url)
        {
            Delegate.OnPageFinished (view, url);
        }
    }
}

