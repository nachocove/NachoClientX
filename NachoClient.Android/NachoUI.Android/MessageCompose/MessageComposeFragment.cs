﻿
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
using Android.Content.PM;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;

using MimeKit;
using Java.Interop;
using NachoPlatform;
using System.IO;
using Android.Views.InputMethods;

namespace NachoClient.AndroidClient
{

    public class MessageComposeFragment :
        Fragment,
        NachoJavascriptMessageHandler,
        MessageComposerDelegate,
        NachoWebClientDelegate,
        MessageComposeHeaderViewDelegate
    {

        private const string FRAGMENT_ACCOUNT_CHOOSER = "NachoClient.AndroidClient.MessageComposeFragment.FRAGMENT_ACCOUNT_CHOOSER";
        private const int REQUEST_CONTACTS_PERMISSIONS = 1;

        #region Properties

        MessageComposer _Composer;
        public MessageComposer Composer {
            get {
                return _Composer;
            }
            set {
                _Composer = value;
                _Composer.Delegate = this;
                BeginComposing ();
            }
        }

        public McAccount Account {
            get {
                if (Composer != null) {
                    return Composer.Account;
                }
                return null;
            }
            set {
                if (Composer == null) {
                    Composer = new MessageComposer (value);
                }
            }
        }

        public bool CanSend { get; private set; }

        bool IsSavingHTML;
        string SavedHTML;
        bool IsWebViewLoaded;
        bool FocusWebViewOnLoad;
        List<Tuple<string, JavascriptCallback>> JavaScriptQueue;

        #endregion

        #region Constructor/Factory

        public MessageComposeFragment () : base ()
        {
            RetainInstance = true;
        }

        #endregion

        #region Subviews

        MessageComposeHeaderView HeaderView;
        Android.Webkit.WebView WebView;

        void FindSubviews (View view)
        {
            HeaderView = view.FindViewById<MessageComposeHeaderView> (Resource.Id.header);
            WebView = view.FindViewById<Android.Webkit.WebView> (Resource.Id.message);

            HeaderView.Delegate = this;

            WebView.Settings.JavaScriptEnabled = true;
            WebView.AddJavascriptInterface (new NachoJavascriptMessenger (this, "nacho"), "_android_messageHandlers_nacho");
            WebView.AddJavascriptInterface (new NachoJavascriptMessenger (this, "nachoCompose"), "_android_messageHandlers_nachoCompose");
            WebView.SetWebViewClient (new NachoWebClient (this));
        }

        void ClearSubviews ()
        {
            HeaderView.Cleanup ();
            HeaderView.Delegate = null;
            WebView.SetWebViewClient (null);
            WebView.RemoveJavascriptInterface ("_android_messageHandlers_nacho");
            WebView.RemoveJavascriptInterface ("_android_messageHandlers_nachoCompose");
            WebView.StopLoading ();
            HeaderView = null;
            WebView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override void OnCreate (Bundle savedInstanceState)
        {
            Log.Info (Log.LOG_UI, "MessageComposeActivity ComposeFragment OnCreate");
            base.OnCreate (savedInstanceState);
            AttachmentPicker.AttachmentPicked += AttachmentPicked;
            if (savedInstanceState != null) {
                Log.Info (Log.LOG_UI, "MessageComposeActivity ComposeFragment savedInstanceState != null");
                AttachmentPicker.OnCreate (savedInstanceState);
            }
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            Log.Info (Log.LOG_UI, "MessageComposeActivity ComposeFragment OnCreateView");
            var view = inflater.Inflate (Resource.Layout.MessageComposeFragment, container, false);

            JavaScriptQueue = new List<Tuple<string, JavascriptCallback>> ();

            FindSubviews (view);

            BeginComposing ();

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            CheckForAndroidPermissions ();
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            Log.Info (Log.LOG_UI, "MessageComposeActivity ComposeFragment OnSaveInstanceState");
            base.OnSaveInstanceState (outState);
            AttachmentPicker.OnSaveInstanceState (outState);
            EndEditing ();
            IsSavingHTML = true;
            GetHtmlContent ((html) => {
                SavedHTML = html;
                IsSavingHTML = false;
                if (WebView != null) {
                    DisplayMessageBody ();
                    SavedHTML = null;
                }
            });
        }

        public override void OnDestroyView ()
        {
            Log.Info (Log.LOG_UI, "MessageComposeActivity ComposeFragment OnDestroyView");
            ClearSubviews ();
            IsWebViewLoaded = false;
            base.OnDestroyView ();
        }

        public override void OnDestroy ()
        {
            AttachmentPicker.AttachmentPicked -= AttachmentPicked;
            base.OnDestroy ();
        }

        void BeginComposing ()
        {
            if (Composer != null) {
                if (SavedHTML != null) {
                    DisplayMessageBody ();
                    SavedHTML = null;
                } else if (!IsSavingHTML) {
                    Composer.StartPreparingMessage ();
                }
                UpdateHeader ();
                UpdateSendEnabled ();
                if (Composer.InitialQuickReply) {
                    Composer.InitialQuickReply = false;
                    ShowQuickResponses ();
                } else {
                    if (!Composer.HasRecipient) {
                        HeaderView.ToField.RequestFocus ();
                    } else if (String.IsNullOrEmpty (Composer.Message.Subject)) {
                        HeaderView.SubjectField.RequestFocus ();
                    } else if (IsWebViewLoaded) {
                        FocusWebView ();
                    } else {
                        FocusWebViewOnLoad = true;
                    }
                }
            }
        }

        #endregion

        #region User Actions

        Action<bool> SendCompletion;

        public void Send (Action<bool> completion)
        {
            SendCompletion = completion;
            EndEditing ();
            if (String.IsNullOrWhiteSpace (Composer.Message.Subject)) {
                var alert = new AlertDialog.Builder (Activity);
                alert.SetTitle (Resource.String.message_compose_empty_subject_title);
                alert.SetMessage (Resource.String.message_compose_empty_subject_message);
                alert.SetNeutralButton (Resource.String.message_compose_empty_subject_send, SendWithoutSubject);
                alert.SetPositiveButton (Resource.String.message_compose_empty_subject_edit, AddSubject);
                alert.ShowWithCancelAction (SendCanceled);
            } else {
                CheckSizeBeforeSending ();
            }
        }

        public void PickAttachment ()
        {
            EndEditing ();
            Save (ShowAttachmentPicker);
        }

        #endregion

        #region Send Process

        void SendCanceled ()
        {
            SendCompletion (false);
            SendCompletion = null;
        }

        void SendWithoutSubject (object sender, EventArgs args)
        {
            CheckSizeBeforeSending ();
        }

        void AddSubject (object sender, EventArgs args)
        {
            HeaderView.FocusSubject ();
            SendCompletion (false);
            SendCompletion = null;
        }

        void CheckSizeBeforeSending ()
        {
            GetHtmlContent ((string html) => {
                Composer.Save (html);
                if (Composer.IsOversize) {
                    if (Composer.CanResize) {
                        var alert = new AlertDialog.Builder (Activity);
                        var format = GetString (Resource.String.message_compose_resize_title_format);
                        alert.SetTitle (String.Format (format, Pretty.PrettyFileSize (Composer.MessageSize)));
                        alert.SetSingleChoiceItems (new string [] {
                            String.Format (GetString (Resource.String.message_compose_resize_choice_small_format), Pretty.PrettyFileSize (Composer.EstimatedSmallSize)),
                            String.Format (GetString (Resource.String.message_compose_resize_choice_medium_format), Pretty.PrettyFileSize (Composer.EstimatedMediumSize)),
                            String.Format (GetString (Resource.String.message_compose_resize_choice_large_format), Pretty.PrettyFileSize (Composer.EstimatedLargeSize)),
                            String.Format (GetString (Resource.String.message_compose_resize_choice_actual_format), Pretty.PrettyFileSize (Composer.MessageSize))
                        }, -1, (object sender, DialogClickEventArgs e) => {
                            switch (e.Which) {
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
                                NcAssert.CaseError ();
                                break;
                            }
                        });
                        alert.ShowWithCancelAction (SendCanceled);
                    } else {
                        var alert = new AlertDialog.Builder (Activity);
                        alert.SetTitle (Resource.String.message_compose_oversize_title);
                        alert.SetMessage (String.Format (GetString (Resource.String.message_compose_oversize_message_format), Pretty.PrettyFileSize (Composer.MessageSize)));
                        alert.SetNeutralButton (Resource.String.message_compose_oversize_message_send, AcknowlegeSizeWarning);
                        alert.ShowWithCancelAction (SendCanceled);
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
            if (SendCompletion != null) {
                SendCompletion (true);
            }
        }

        #endregion

        #region Attachment Picking

        AttachmentPicker AttachmentPicker = new AttachmentPicker ();

        void ShowAttachmentPicker ()
        {
            AttachmentPicker.Show (this, Composer.Account.Id);
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            if (AttachmentPicker.OnActivityResult (this, Composer.Account.Id, requestCode, resultCode, data)) {
                return;
            }
        }

        void AttachmentPicked (object sender, McAttachment attachment)
        {
            attachment.Link (Composer.Message);
            HeaderView.AddAttachment (attachment);
        }

        #endregion

        #region User Action - Header

        bool SalesforceBccAdded = false;
        Dictionary<string, bool> SalesforceAddressCache = new Dictionary<string, bool> ();

        void AddSalesforceBccIfNeeded ()
        {
            if (!SalesforceBccAdded) {
                string extraBcc = EmailHelper.ExtraSalesforceBccAddress (SalesforceAddressCache, Composer.Message);
                if (null != extraBcc) {
                    SalesforceBccAdded = true;
                    if (string.IsNullOrEmpty (Composer.Message.Bcc)) {
                        Composer.Message.Bcc = extraBcc;
                    } else {
                        Composer.Message.Bcc += ", " + extraBcc;
                    }
                    UpdateHeaderBccView ();
                }
            }
        }


        public void MessageComposeHeaderViewDidChangeTo (MessageComposeHeaderView view, string to)
        {
            Composer.Message.To = to;
            AddSalesforceBccIfNeeded ();
            UpdateSendEnabled ();
        }

        public void MessageComposeHeaderViewDidChangeCc (MessageComposeHeaderView view, string cc)
        {
            Composer.Message.Cc = cc;
            AddSalesforceBccIfNeeded ();
            UpdateSendEnabled ();
        }

        public void MessageComposeHeaderViewDidChangeBcc (MessageComposeHeaderView view, string bcc)
        {
            Composer.Message.Bcc = bcc;
            AddSalesforceBccIfNeeded ();
            UpdateSendEnabled ();
        }

        public void MessageComposeHeaderViewDidChangeSubject (MessageComposeHeaderView view, string subject)
        {
            Composer.Message.Subject = subject;
        }

        public void MessageComposeHeaderViewDidSelectFromField (MessageComposeHeaderView view, string from)
        {
            EndEditing ();
            var accountChooser = new AccountChooserFragment ();
            accountChooser.Show (FragmentManager, FRAGMENT_ACCOUNT_CHOOSER, Composer.Account, () => {
                var selectedAccount = accountChooser.SelectedAccount;
                if (selectedAccount.Id != Composer.Account.Id) {
                    Composer.SetAccount (selectedAccount);
                    var mailbox = new MailboxAddress (Pretty.UserNameForAccount (Composer.Account), Composer.Account.EmailAddr);
                    Composer.Message.From = mailbox.ToString ();
                    UpdateHeaderFromView ();
                }
            });
        }

        public void MessageComposeHeaderViewDidSelectAddAttachment (MessageComposeHeaderView view)
        {
            PickAttachment ();
        }

        public void MessageComposeHeaderViewDidSelectAttachment (MessageComposeHeaderView view, McAttachment attachment)
        {
            AttachmentHelper.OpenAttachment (Activity, attachment, true);
        }

        public void MessageComposeHeaderViewDidRemoveAttachment (MessageComposeHeaderView view, McAttachment attachment)
        {
            attachment.Unlink (Composer.Message);
        }

        //public void QuickResponseFragmentDidSelectResponse (QuickResponseFragment fragment, NcQuickResponse.QuickResponse response)
        //{
        //    if (!EmailHelper.IsReplyAction (Composer.Kind) && !EmailHelper.IsForwardAction (Composer.Kind)) {
        //        Composer.Message.Subject = response.subject;
        //        UpdateHeaderSubjectView ();
        //    }
        //    if (Composer.IsMessagePrepared) {
        //        var javascriptString = JavaScriptEscapedString (response.body + Composer.SignatureText ());
        //        EvaluateJavascript (String.Format ("Editor.defaultEditor.replaceUserText({0});", javascriptString));
        //    } else {
        //        Composer.InitialText = response.body;
        //    }
        //    if (response.intent != null) {
        //        Composer.Message.Intent = response.intent.type;
        //    } else {
        //        Composer.Message.Intent = McEmailMessage.IntentType.None;
        //    }
        //    Composer.Message.IntentDate = DateTime.MinValue;
        //    Composer.Message.IntentDateType = MessageDeferralType.None;
        //    UpdateHeaderIntentView ();
        //    //HeaderView.ShowIntentField ();
        //}

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
            if (IsWebViewLoaded) {
                return;
            }
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
            WebView.FocusChange += WebViewFocused;
            WebView.RequestFocus ();
        }

        void WebViewFocused (object sender, Android.Views.View.FocusChangeEventArgs e)
        {
            var scrollView = View.FindViewById (Resource.Id.scroll_view) as Android.Support.V4.Widget.NestedScrollView;
            EvaluateJavascript ("Editor.defaultEditor.focus()", (result) => {
                scrollView.ScrollY = 0;
            });
            scrollView.ScrollY = 0;
            WebView.FocusChange -= WebViewFocused;
        }

        #endregion

        #region Message Composer

        public void MessageComposerDidCompletePreparation (MessageComposer composer)
        {
            DisplayMessageBody ();
        }

        public void MessageComposerDidFailToLoadMessage (MessageComposer composer)
        {
            if (null != this.Activity) {
                NcAlertView.ShowMessage (this.Activity, "Could not load message", "Sorry, we could not load your message. Please try again.");
            }
        }

        void DisplayMessageBody ()
        {
            if (Composer.Bundle != null) {
                if (SavedHTML != null) {
                    WebView.LoadDataWithBaseURL (Composer.Bundle.BaseUrl.AbsoluteUri, SavedHTML, "text/html", "utf-8", null);
                } else if (Composer.Bundle.FullHtmlUrl != null) {
                    WebView.LoadUrl (Composer.Bundle.FullHtmlUrl.AbsoluteUri);
                } else {
                    var html = Composer.Bundle.FullHtml;
                    WebView.LoadDataWithBaseURL (Composer.Bundle.BaseUrl.AbsoluteUri, html, "text/html", "utf-8", null);
                }
            }
        }

        #endregion

        #region View Updates

        private void UpdateSendEnabled ()
        {
            CanSend = Composer.HasRecipient;
            Activity.InvalidateOptionsMenu ();
        }

        private void UpdateHeader ()
        {
            HeaderView.ToField.AddressField.AddressString = Composer.Message.To;
            HeaderView.CcField.AddressField.AddressString = Composer.Message.Cc;
            UpdateHeaderBccView ();
            UpdateHeaderFromView ();
            UpdateHeaderSubjectView ();
            UpdateHeaderIntentView ();
            UpdateHeaderAttachmentsView ();
        }

        void UpdateHeaderFromView ()
        {
            HeaderView.SetFromValue (Composer.Message.From);
        }

        void UpdateHeaderBccView ()
        {
            HeaderView.BccField.AddressField.AddressString = Composer.Message.Bcc;
        }

        void UpdateHeaderSubjectView ()
        {
            HeaderView.SubjectField.TextField.Text = Composer.Message.Subject;
        }

        void UpdateHeaderIntentView ()
        {
            //HeaderView.IntentValueLabel.Text = NcMessageIntent.GetIntentString (Composer.Message.Intent, Composer.Message.IntentDateType, Composer.Message.IntentDate);
        }

        private void UpdateHeaderAttachmentsView ()
        {
            var attachments = McAttachment.QueryByItem (Composer.Message);
            HeaderView.SetAttachments (attachments);
        }

        #endregion

        #region Draft Management

        public void Discard ()
        {
            Composer.Message.Delete ();
        }

        public void Save (Action callback)
        {
            GetHtmlContent ((string html) => {
                Composer.Save (html);
                callback ();
            });
        }

        delegate void HtmlContentCallback (string html);

        void GetHtmlContent (HtmlContentCallback callback)
        {
            EvaluateJavascript ("document.documentElement.outerHTML", (Java.Lang.Object result) => {
                var stringResult = result as Java.Lang.String;
                // I don't know why the result is coming in as a JSON-encoded string value, but it is
                var json = stringResult.ToString ();
                var str = System.Json.JsonValue.Parse (json);
                callback ("<!DOCTYPE html>\n" + str);
            });
        }

        #endregion

        #region Private Helpers

        public void EndEditing ()
        {
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (View.WindowToken, HideSoftInputFlags.NotAlways);
        }

        string JavaScriptEscapedString (string s)
        {
            var primitive = new System.Json.JsonPrimitive (s);
            string escaped = "";
            using (var writer = new StringWriter ()) {
                primitive.Save (writer);
                escaped = writer.ToString ();
            }
            return escaped.Replace ("\n", "\\n");
        }

        void ShowQuickResponses ()
        {
            NcQuickResponse.QRTypeEnum responseType = NcQuickResponse.QRTypeEnum.Compose;

            if (EmailHelper.IsReplyAction (Composer.Kind)) {
                responseType = NcQuickResponse.QRTypeEnum.Reply;
            } else if (EmailHelper.IsForwardAction (Composer.Kind)) {
                responseType = NcQuickResponse.QRTypeEnum.Forward;
            }

            // TODO: new quick response picker
        }

        #endregion

        #region Bride JS Result Interface to Delegate

        delegate void JavascriptCallback (Java.Lang.Object value);

        class JavascriptResultHandler : Java.Lang.Object, Android.Webkit.IValueCallback
        {

            JavascriptCallback Callback;

            public JavascriptResultHandler (JavascriptCallback callback) : base ()
            {
                Callback = callback;
            }

            public void OnReceiveValue (Java.Lang.Object value)
            {
                Callback (value);
            }

        }

        #endregion

        #region Permissions

        void CheckForAndroidPermissions ()
        {
            // Check is always called when the the compose view appears.  The goal here is to ask only if we've never asked before
            // On Android, "never asked before" means:
            // 1. We don't have permission
            // 2. ShouldShowRequestPermissionRationale returns false
            //    (Android only instructs us to show a rationale if we've prompted once and the user has denied the request)
            bool hasAndroidReadPermission = Android.Support.V4.Content.ContextCompat.CheckSelfPermission (Activity, Android.Manifest.Permission.ReadContacts) == Permission.Granted;
            bool hasAndroidWritePermission = Android.Support.V4.Content.ContextCompat.CheckSelfPermission (Activity, Android.Manifest.Permission.WriteContacts) == Permission.Granted;
            if (!hasAndroidReadPermission || !hasAndroidWritePermission) {
                bool hasAskedRead = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.ReadContacts);
                bool hasAskedWrite = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.WriteContacts);
                if (!hasAskedRead && !hasAskedWrite) {
                    RequestAndroidPermissions ();
                }
            }
        }

        void RequestAndroidPermissions ()
        {
            bool shouldAskRead = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.ReadContacts);
            bool shouldAskWrite = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.WriteContacts);
            if (shouldAskRead || shouldAskWrite) {
                var builder = new Android.App.AlertDialog.Builder (Context);
                builder.SetTitle (Resource.String.contacts_permission_request_title);
                builder.SetMessage (Resource.String.contacts_permission_request_message);
                builder.SetNegativeButton (Resource.String.contacts_permission_request_cancel, (sender, e) => { });
                builder.SetPositiveButton (Resource.String.contacts_permission_request_ack, (sender, e) => {
                    RequestPermissions (new string [] {
                        Android.Manifest.Permission.ReadContacts,
                        Android.Manifest.Permission.WriteContacts
                    }, REQUEST_CONTACTS_PERMISSIONS);
                });
                builder.Show ();
            } else {
                RequestPermissions (new string [] {
                    Android.Manifest.Permission.ReadContacts,
                    Android.Manifest.Permission.WriteContacts
                }, REQUEST_CONTACTS_PERMISSIONS);
            }
        }

        public override void OnRequestPermissionsResult (int requestCode, string [] permissions, Permission [] grantResults)
        {
            if (requestCode == REQUEST_CONTACTS_PERMISSIONS) {
                if (grantResults.Length == 2 && grantResults [0] == Permission.Granted && grantResults [1] == Permission.Granted) {
                    BackEnd.Instance.Start (McAccount.GetDeviceAccount ().Id);
                } else {
                    // If the user denies one or both of the permissions, re-request, this time shownig our rationale.
                    bool shouldAskRead = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.ReadContacts);
                    bool shouldAskWrite = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.WriteContacts);
                    if (shouldAskRead || shouldAskWrite) {
                        RequestAndroidPermissions ();
                    }
                }
            }
            base.OnRequestPermissionsResult (requestCode, permissions, grantResults);
        }

        #endregion

    }

    public interface NachoJavascriptMessageHandler
    {

        void HandleJavascriptMessage (NachoJavascriptMessage message);

    }

    public class NachoJavascriptMessage
    {

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

    interface NachoWebClientDelegate
    {

        void OnPageFinished (Android.Webkit.WebView view, string url);

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

