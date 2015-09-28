// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using System.Text;
using CoreGraphics;
using Foundation;
using UIKit;

using MimeKit;

using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;
using NachoCore.Utils;

using WebKit;

namespace NachoClient.iOS
{

    public interface MessageComposeViewDelegate {

        void MessageComposeViewDidBeginSend (MessageComposeViewController vc);
        void MessageComposeViewDidSaveDraft (MessageComposeViewController vc);
        void MessageComposeViewDidCancel (MessageComposeViewController vc);

    }

    public partial class MessageComposeViewController : NcUIViewController,
        IWKNavigationDelegate,
        IWKScriptMessageHandler,
        IUIWebViewDelegate,
        IUIScrollViewDelegate,
        MessageComposeHeaderViewDelegate,
        QuickResponseViewControllerDelegate,
        INachoIntentChooserParent,
        INachoDateControllerParent,
        INachoFileChooserParent,
        INachoContactChooserDelegate,
        MessageComposerDelegate
    {

        #region Properties

        public MessageComposeViewDelegate ComposeDelegate;
        public bool StartWithQuickResponse;
        public readonly MessageComposer Composer;
        CompoundScrollView ScrollView;
        MessageComposeHeaderView HeaderView;
        UIWebView WebView;
        NcUIBarButtonItem CloseButton;
        NcUIBarButtonItem SendButton;
        NcUIBarButtonItem QuickResponseButton;
        UIAlertController CloseAlertController;
        bool HasShownOnce;
        UIStoryboard mainStorybaord;
        UIStoryboard MainStoryboard {
            get {
                if (mainStorybaord == null) {
                    mainStorybaord = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
                }
                return mainStorybaord;
            }

        }

        NSObject BackgroundNotification;
        NSObject ContentSizeCategoryChangedNotification;

        protected static readonly long EMAIL_SIZE_ALERT_LIMIT = 2000000;

        #endregion

        #region Constructors

        public MessageComposeViewController () : base ()
        {
            Composer = new MessageComposer (NcApplication.Instance.Account);
            Composer.Delegate = this;
        }

        #endregion

        #region Presenters

        public void Present (Action completionHandler = null)
        {
            var window = UIApplication.SharedApplication.Delegate.GetWindow ();
            var navigationController = new UINavigationController (this);
            NachoClient.Util.ConfigureNavBar (false, navigationController);
            window.RootViewController.PresentViewController (navigationController, true, completionHandler);
        }

        #endregion

        #region View Lifecycle

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            View.BackgroundColor = UIColor.White;

            // Nav bar
            CloseButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (CloseButton, "icn-close");
            CloseButton.AccessibilityLabel = "Close";
            CloseButton.Clicked += Close;

            SendButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (SendButton, "icn-send");
            SendButton.AccessibilityLabel = "Send";
            SendButton.Clicked += Send;

            QuickResponseButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (QuickResponseButton, "contact-quickemail");
            QuickResponseButton.AccessibilityLabel = "Quick response";
            QuickResponseButton.Clicked += QuickReply;

            NavigationItem.LeftBarButtonItem = CloseButton;
            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                SendButton,
                QuickResponseButton,
            };

            // Content Area
            ScrollView = new CompoundScrollView (View.Bounds);
            ScrollView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            ScrollView.AlwaysBounceVertical = true;
            ScrollView.Delegate = this;

            HeaderView = new MessageComposeHeaderView (ScrollView.Bounds);
            HeaderView.Frame = new CGRect (0.0, 0.0, ScrollView.Bounds.Width, HeaderView.PreferredHeight);
            HeaderView.HeaderDelegate = this;
            HeaderView.AttachmentsAllowed = Composer.RelatedCalendarItem == null;
//            var config = new WKWebViewConfiguration ();
//            config.SuppressesIncrementalRendering = true;
//            config.UserContentController.AddScriptMessageHandler (this, "nachoCompose");
//            config.UserContentController.AddScriptMessageHandler (this, "nacho");
//            WebView = new WKWebView (ScrollView.Bounds, config);
//            WebView.NavigationDelegate = this;
            WebView = new UIWebView (View.Bounds);
            WebView.SuppressesIncrementalRendering = true;
            WebView.Delegate = this;

            ScrollView.AddCompoundView (HeaderView);
            ScrollView.AddCompoundView (WebView);
            View.AddSubview (ScrollView);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            Composer.StartPreparingMessage ();
            UpdateHeaderView ();
            RegisterForNotifications ();
            if (!HasShownOnce) {
                if (StartWithQuickResponse) {
                    ShowQuickResponses ();
                }
                HasShownOnce = true;
            }

        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            UnregisterNotifications ();
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
        }

        #endregion

        #region Layout

        private void UpdateScrollViewSize ()
        {
            ScrollView.DetermineContentSize ();
        }

        private void LayoutScrollView ()
        {
            UpdateScrollViewSize ();
            ScrollView.SetNeedsLayout ();
            ScrollView.LayoutIfNeeded ();
        }

        public void MessageComposeHeaderViewDidChangeHeight (MessageComposeHeaderView view)
        {
            LayoutScrollView ();
        }

        protected override void OnKeyboardChanged ()
        {
            var frame = View.Bounds;
            frame.Height = frame.Height - keyboardHeight;
            ScrollView.Frame = frame;
            ScrollView.SetNeedsLayout ();
            ScrollView.LayoutIfNeeded ();
        }

        #endregion

        #region User Actions - Navbar

        // User hitting the send button
        public void Send (object sender, EventArgs e)
        {
            View.EndEditing (true);
            // TODO: preflight checks like size
            // TODO: offer option to resize images
            // TODO: send (can happen in background)
            var html = GetHtmlContent ();
            Composer.Send (html);
            if (ComposeDelegate != null) {
                ComposeDelegate.MessageComposeViewDidBeginSend (this);
            }
            DismissViewController (true, null);
        }

        // User hitting the quick reply button
        public void QuickReply (object sender, EventArgs e)
        {
            View.EndEditing (true);
            ShowQuickResponses ();
        }

        // User hitting the close button
        public void Close (object sender, EventArgs e)
        {
            View.EndEditing (true);
            CloseAlertController = UIAlertController.Create (null, null, UIAlertControllerStyle.ActionSheet);
            CloseAlertController.AddAction (UIAlertAction.Create ("Discard Draft", UIAlertActionStyle.Destructive, Discard));
            CloseAlertController.AddAction (UIAlertAction.Create ("Save Draft", UIAlertActionStyle.Default, Save));
            CloseAlertController.AddAction (UIAlertAction.Create ("Cancel", UIAlertActionStyle.Cancel, (UIAlertAction obj) => { CloseAlertController = null; }));
            PresentViewController (CloseAlertController, true, null);
        }

        // User opting to discard while closing
        public void Discard (UIAlertAction obj)
        {
            CloseAlertController = null;
            Composer.Message.Delete ();
            if (ComposeDelegate != null) {
                ComposeDelegate.MessageComposeViewDidCancel (this);
            }
            DismissViewController (true, null);
        }

        // User opting to save while closing
        public void Save (UIAlertAction obj)
        {
            CloseAlertController = null;
            var html = GetHtmlContent ();
            Composer.Save (html);
            if (ComposeDelegate != null) {
                ComposeDelegate.MessageComposeViewDidCancel (this);
            }
            DismissViewController (true, null);
        }

        // User selecting a quick response
        public void QuickResponseViewDidSelectResponse (QuickResponseViewController vc, NcQuickResponse.QRTypeEnum whatType, NcQuickResponse.QuickResponse response, McEmailMessage.IntentType intentType)
        {
            if (whatType == NcQuickResponse.QRTypeEnum.Compose) {
                Composer.Message.Subject = response.subject;
                UpdateHeaderSubjectView ();
            }
            if (Composer.IsMessagePrepared) {
                // TODO: need to insert response.body into webview
                // Web view may not have loaded yet, even if the composer is all done
            } else {
                Composer.InitialText = response.body;
            }
            // TODO: show the intent field if hidden
            Composer.Message.Intent = intentType;
            Composer.Message.IntentDate = DateTime.MinValue;
            Composer.Message.IntentDateType = MessageDeferralType.None;
            UpdateHeaderIntentView ();
        }

        #endregion

        #region User Actions - Header

        // User selecting + button in To/CC/BCC field
        public void MessageComposeHeaderViewDidSelectContactChooser (MessageComposeHeaderView view, NcEmailAddress address)
        {
            ContactChooserViewController chooserController = MainStoryboard.InstantiateViewController ("ContactChooserViewController") as ContactChooserViewController;
            chooserController.SetOwner (this, Composer.Account, address, NachoContactType.EmailRequired);
            FadeCustomSegue.Transition (this, chooserController);
        }

        // User starting to type in To/CC/BCC field
        public void MessageComposeHeaderViewDidSelectContactSearch (MessageComposeHeaderView view, NcEmailAddress address)
        {
            ContactSearchViewController searchController = MainStoryboard.InstantiateViewController ("ContactSearchViewController") as ContactSearchViewController;
            searchController.SetOwner (this, Composer.Account, address, NachoContactType.EmailRequired);
            FadeCustomSegue.Transition (this, searchController);
        }

        // User selecting contact for To/CC/BCC field
        public void UpdateEmailAddress (INachoContactChooser vc, NcEmailAddress address)
        {
            if (address.kind == NcEmailAddress.Kind.To) {
                HeaderView.ToView.Append (address);
                Composer.Message.To = EmailHelper.AddressStringFromList (HeaderView.ToView.AddressList);
            } else if (address.kind == NcEmailAddress.Kind.Cc) {
                HeaderView.CcView.Append (address);
                Composer.Message.Cc = EmailHelper.AddressStringFromList (HeaderView.CcView.AddressList);
            } else if (address.kind == NcEmailAddress.Kind.Bcc) {
                HeaderView.BccView.Append (address);
                Composer.Message.Bcc = EmailHelper.AddressStringFromList (HeaderView.BccView.AddressList);
            } else {
                NcAssert.CaseError ();
            }
        }

        public void MessageComposeHeaderViewDidRemoveAddress (MessageComposeHeaderView view, NcEmailAddress address)
        {
            
            if (address.kind == NcEmailAddress.Kind.To) {
                Composer.Message.To = EmailHelper.AddressStringFromList (HeaderView.ToView.AddressList);
            } else if (address.kind == NcEmailAddress.Kind.Cc) {
                Composer.Message.Cc = EmailHelper.AddressStringFromList (HeaderView.CcView.AddressList);
            } else if (address.kind == NcEmailAddress.Kind.Bcc) {
                Composer.Message.Bcc = EmailHelper.AddressStringFromList (HeaderView.BccView.AddressList);
            } else {
                NcAssert.CaseError ();
            }
        }

        // ??
        // I think this is when the user starts typing an email adderess and then clears it.
        // Since we don't change anything when they start typing, there's nothing to change if the clear.
        public void DeleteEmailAddress (INachoContactChooser vc, NcEmailAddress address)
        {
            // old implementation did nothing
        }

        // User changing the subject
        public void MessageComposeHeaderViewDidChangeSubject (MessageComposeHeaderView view, string subject)
        {
            Composer.Message.Subject = subject;
        }

        // User tapping the intent field 
        public void MessageComposeHeaderViewDidSelectIntentField (MessageComposeHeaderView view)
        {
            IntentSelectionViewController intentController = MainStoryboard.InstantiateViewController ("IntentSelectionViewController") as IntentSelectionViewController;
            intentController.ModalTransitionStyle = UIModalTransitionStyle.CrossDissolve;
            intentController.SetOwner (this);
            intentController.SetDateControllerOwner (this);
            PresentViewController (intentController, true, null);
        }

        // User selecting an intent
        public void SelectMessageIntent (NcMessageIntent.MessageIntent intent)
        {
            Composer.Message.Intent = intent.type;
            Composer.Message.IntentDateType = MessageDeferralType.None;
            Composer.Message.IntentDate = DateTime.MinValue;
            UpdateHeaderIntentView ();
        }

        // User selecting a date for the intent
        public void DateSelected (NcMessageDeferral.MessageDateType type, MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate)
        {
            Composer.Message.IntentDateType = request;
            Composer.Message.IntentDate = selectedDate;
            UpdateHeaderIntentView ();
        }
            
        // User tapping on add attachment
        public void MessageComposeHeaderViewDidSelectAddAttachment (MessageComposeHeaderView view)
        {
            AddAttachmentViewController attachmentViewController = MainStoryboard.InstantiateViewController ("AddAttachmentViewController") as AddAttachmentViewController;
            attachmentViewController.ModalTransitionStyle = UIModalTransitionStyle.CrossDissolve;
            attachmentViewController.SetOwner (this, Composer.Account);
            PresentViewController (attachmentViewController, true, null);
        }

        // User tapping on a specific attachment to display
        public void MessageComposeHeaderViewDidSelectAttachment (MessageComposeHeaderView view, McAttachment attachment)
        {
            PlatformHelpers.DisplayAttachment (this, attachment);
        }

        // User picking a file as an attachment
        public void SelectFile (INachoFileChooser vc, McAbstrObject obj)
        {
            var attachment = obj as McAttachment;
            if (attachment != null) {
                attachment = AttachmentHelper.CopyAttachment (attachment);
            }else{
                var file = obj as McDocument;
                if (file != null) {
                    attachment = McAttachment.InsertSaveStart (Composer.Account.Id);
                    attachment.SetDisplayName (file.DisplayName);
                    attachment.IsInline = true;
                    attachment.UpdateFileCopy (file.GetFilePath ());
                } else {
                    var note = obj as McNote;
                    if (note != null) {
                        attachment = McAttachment.InsertSaveStart (Composer.Account.Id);
                        attachment.SetDisplayName (note.DisplayName + ".txt");
                        attachment.IsInline = true;
                        attachment.UpdateData (note.noteContent);
                    }
                }
            }

            if (attachment != null) {
                attachment.ItemId = Composer.Message.Id;
                attachment.Update ();
                HeaderView.AttachmentsView.Append (attachment);
                this.DismissViewController (true, null);
            } else {
                NcAssert.CaseError ();
            }

        }

        // User deleting an attachment
        public void MessageComposeHeaderViewDidRemoveAttachment (MessageComposeHeaderView view, McAttachment attachment)
        {
            attachment.Delete ();
        }

        // User adding an attachment from media browser
        public void Append (McAttachment attachment)
        {
            attachment.ItemId = Composer.Message.Id;
            attachment.Update ();
            HeaderView.AttachmentsView.Append (attachment);
        }

        // Not really a direct user action, but caused by the user selecting a date for the intent
        public void DismissChildDateController (INachoDateController vc)
        {
            // Basically, once the intent date view controller is dismissed, we need to dismiss the intent controller
            DismissViewController (false, null);
        }

        // Not really a direct user action, but caused by the user selecting a date for the intent
        public void DismissChildFileChooser (INachoFileChooser vc)
        {
            DismissViewController (true, null);
        }

        // Not really a direct user action, but caused by the user selecting a date for the intent
        public void DismissPhotoPicker ()
        {
            DismissViewController (true, null);
        }

        // Not really a direct user action, but caused by the user selecting a contact
        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            // The contact chooser was pushed on the nav stack, rather than shown as a modal.
            // So we need to pop it from the stack
            vc.Cleanup ();
            NavigationController.PopViewController (true);
        }

        #endregion

        #region Message Preparation

        public void MessageComposerDidCompletePreparation (MessageComposer composer)
        {
            DisplayMessageBody ();
        }

        void DisplayMessageBody ()
        {
            if (Composer.Bundle != null) {
                if (Composer.Bundle.FullHtmlUrl != null) {
                    var url = new NSUrl (Composer.Bundle.FullHtmlUrl.AbsoluteUri);
//                    if (url.Scheme.ToLowerInvariant().Equals("file")){
//                        var selector = new ObjCRuntime.Selector ("loadFileURL:allowingReadAccessToURL:");
//                        if (WebView.RespondsToSelector (selector)) {
//                            var baseUrl = new NSUrl (Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments));
//                            WebView.PerformSelector (selector, url, baseUrl);
//                        } else {
//                            // need a workaround for iOS 8
//                            // - can run an http server
//                            // - can copy files to /tmp
//                            // - curious about symlink from /tmp -> Documents, but doubtful it will work
//                        }
//                    } else {
//                        NSUrlRequest request = new NSUrlRequest (url);
//                        WebView.LoadRequest (request);
//                    }

                    NSUrlRequest request = new NSUrlRequest (url);
                    WebView.LoadRequest (request);
                } else {
                    var html = Composer.Bundle.FullHtml;
                    var url = new NSUrl (Composer.Bundle.BaseUrl.AbsoluteUri);
                    WebView.LoadHtmlString (new NSString(html), url);
                }
            }
        }

        #endregion

        #region Web View Delegate

        [Export ("webView:didFinishNavigation:")]
        public void DidFinishNavigation (WebKit.WKWebView webView, WebKit.WKNavigation navigation)
        {
            // The navigation is done, meaning the HTML has loaded in the web view, so we now have to
            // tell our scroll view how big the webview is.
            // Unfortunately, WebView.ScrollView.ContentSize.Height is still 0 at this point.
            // It's a timing issue, and so we'll wait until it's not 0
            UpdateScrollViewSizeOnceWebViewIsSized ();
            EnableEditingInWebView ();
        }

        [Export ("updateScrollViewSizeOnceWebViewIsSized")]
        private void UpdateScrollViewSizeOnceWebViewIsSized ()
        {
            // The basic idea is to keep scheduling ourselves in the run loop until we see a non-zero height.
            // Using the run loop is crucuial because it means we won't block anything.
            // Experiements show this usually takes anywhere from 1-4 itereations.
            if (WebView.ScrollView.ContentSize.Height == 0.0) {
                var selector = new ObjCRuntime.Selector ("updateScrollViewSizeOnceWebViewIsSized");
                var timer = NSTimer.CreateTimer (0.0, this, selector, null, false);
                NSRunLoop.Main.AddTimer (timer, NSRunLoopMode.Default);
            } else {
                UpdateScrollViewSize ();
            }
        }

        [Export ("userContentController:didReceiveScriptMessage:")]
        public void DidReceiveScriptMessage (WKUserContentController userContentController, WKScriptMessage message)
        {
            NSDictionary body = message.Body as NSDictionary;
            string kind = body.ObjectForKey (new NSString("kind")).ToString ();
            if (message.Name == "nacho") {
                if (kind == "error") {
                    string errorMessage = body.ObjectForKey (new NSString("message")).ToString ();
                    string filename = body.ObjectForKey (new NSString("filename")).ToString ();
                    string lineno = body.ObjectForKey (new NSString("lineno")).ToString ();
                    string colno = body.ObjectForKey (new NSString("colno")).ToString ();
                    Log.Error(Log.LOG_UI, "MessageComposeView javascript uncaught error: [{1}:{2}:{3}] {0}", errorMessage, filename, lineno, colno);
                }
            } else if (message.Name == "nachoCompose") {
                if (kind == "editor-height-changed") {
                    UpdateScrollViewSize ();
                }
            }
        }

        [Export ("webViewDidFinishLoad:")]
        public void LoadingFinished (UIWebView webView)
        {
            UpdateScrollViewSize ();
            EnableEditingInWebView ();
        }

        private void EnableEditingInWebView ()
        {
            EvaluateJavaScript ("Editor.Enable()");
        }

        private void EvaluateJavaScript(string javascript, WKJavascriptEvaluationResult callback = null)
        {
//            WebView.EvaluateJavaScript (new NSString(javascript), (NSObject result, NSError error) => {
//                if (error !=  null){
//                    Log.Error(Log.LOG_UI, "MessageComposeView error evaluating javascript '{0}': {1}", javascript, error);
//                }
//                if (callback != null) {
//                    callback (result, error);
//                }
//            });
            WebView.EvaluateJavascript (javascript);
        }

        [Foundation.Export("scrollViewWillBeginDragging:")]
        public void DraggingStarted (UIScrollView scrollView)
        {
            UpdateScrollViewSize ();
        }

//        [Foundation.Export("scrollViewDidScroll:")]
//        public void Scrolled (UIScrollView scrollView)
//        {
//            var top = WebView.EvaluateJavascript ("window.getSelection().getRangeAt(0).getBoundingClientRect().top");
//            var height = WebView.EvaluateJavascript ("window.innerHeight");
//            var offset = WebView.EvaluateJavascript ("window.pageYOffset");
//            Log.Info (Log.LOG_UI, "MessageComposeView scroll height: {0}, offset: {1}, top: {2}", height, offset, top);
//        }

        #endregion

        #region Helpers

        private void ShowQuickResponses (bool animated = true)
        {
            NcQuickResponse.QRTypeEnum responseType = NcQuickResponse.QRTypeEnum.Compose;

            if (EmailHelper.IsReplyAction (Composer.Kind)) {
                responseType = NcQuickResponse.QRTypeEnum.Reply;
            } else if (EmailHelper.IsForwardAction (Composer.Kind)) {
                responseType = NcQuickResponse.QRTypeEnum.Forward;
            }

            var quickViewController = new QuickResponseViewController ();
            quickViewController.SetProperties (responseType);
            PresentViewController (quickViewController, animated, null);
        }

        private string GetHtmlContent ()
        {
            // This is a sync call with UIWebView, but will be async with WKWebView,
            // which could cause havoc with the design of upstream callers
            return "<!DOCTYPE html>\n" + WebView.EvaluateJavascript ("document.documentElement.outerHTML");
        }

        #endregion

        #region Header View

        private void UpdateHeaderView ()
        {
            UpdateHeaderSubjectView ();
            UpdateHeaderToView ();
            UpdateHeaderCcView ();
            UpdateHeaderBccView ();
            UpdateHeaderIntentView ();
            UpdateHeaderAttachmentsView ();
        }

        private void UpdateHeaderToView ()
        {
            HeaderView.ToView.Clear ();
            var addresses = EmailHelper.AddressList (NcEmailAddress.Kind.To, null, Composer.Message.To);
            foreach (var address in addresses) {
                HeaderView.ToView.Append (address);
            }
        }

        private void UpdateHeaderCcView ()
        {
            HeaderView.CcView.Clear ();
            var addresses = EmailHelper.AddressList (NcEmailAddress.Kind.Cc, null, Composer.Message.Cc);
            foreach (var address in addresses) {
                HeaderView.CcView.Append (address);
            }
        }

        private void UpdateHeaderBccView ()
        {
            HeaderView.BccView.Clear ();
            var addresses = EmailHelper.AddressList (NcEmailAddress.Kind.Bcc, null, Composer.Message.Bcc);
            foreach (var address in addresses) {
                HeaderView.BccView.Append (address);
            }
        }

        private void UpdateHeaderSubjectView ()
        {
            HeaderView.SubjectField.Text = Composer.Message.Subject;
        }

        private void UpdateHeaderIntentView ()
        {
            HeaderView.IntentView.ValueLabel.Text = NcMessageIntent.GetIntentString (Composer.Message.Intent, Composer.Message.IntentDateType, Composer.Message.IntentDate);
        }

        private void UpdateHeaderAttachmentsView ()
        {
            HeaderView.AttachmentsView.Clear ();
            var attachments = McAttachment.QueryByItemId (Composer.Message);
            foreach (var attachment in attachments) {
                HeaderView.AttachmentsView.Append (attachment);
            }
        }

        #endregion

        #region Notifications

        private void RegisterForNotifications ()
        {
            BackgroundNotification = NSNotificationCenter.DefaultCenter.AddObserver (UIApplication.DidEnterBackgroundNotification, OnBackgroundNotification);
            ContentSizeCategoryChangedNotification = NSNotificationCenter.DefaultCenter.AddObserver (UIApplication.ContentSizeCategoryChangedNotification, OnContentSizeCategoryChangedNotification);
        }

        private void UnregisterNotifications ()
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver (BackgroundNotification);
            NSNotificationCenter.DefaultCenter.RemoveObserver (ContentSizeCategoryChangedNotification);
        }

        private void OnBackgroundNotification (NSNotification notification)
        {
            if (null != CloseAlertController) {
                CloseAlertController.DismissViewController (false, null);
            }
        }

        void OnContentSizeCategoryChangedNotification (NSNotification notification)
        {
            // TODO: can we do anything to update the webview font size?
        }

        #endregion

        public override bool ShouldEndEditing {
            get {
                return false;
            }
        }

    }
        
}
