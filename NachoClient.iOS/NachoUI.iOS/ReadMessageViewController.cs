// This file has been autogenerated from a class added in the UI designer.

using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MimeKit;

namespace NachoClient.iOS
{
    public partial class ReadMessageViewController : DialogViewController, INachoMessageControllerDelegate
    {
        public int ThreadIndex;
        public INachoEmailMessages messages;

        public ReadMessageViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            Pushing = true;

            // Multiple buttons spaced evently
            ToolbarItems = new UIBarButtonItem[] {
                flexibleSpaceButton,
                forwardButton,
                flexibleSpaceButton,
                replyAllButton,
                flexibleSpaceButton,
                replyButton,
                flexibleSpaceButton,
            };

            // Multiple buttons on the right side
            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                deleteButton,
                checkButton,
                clockButton,
                listButton
            };

            clockButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageToMessagePriority", this);
            };
            listButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageToMessageAction", this);
            };
            replyButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageToCompose", ComposeViewController.Reply);
            };
            replyAllButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageToCompose", ComposeViewController.ReplyAll);
            };
            forwardButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageToCompose", ComposeViewController.Forward);
            };
            deleteButton.Clicked += (object sender, EventArgs e) => {
                DeleteThisMessage();
            };
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            NavigationController.ToolbarHidden = false;

            MarkAsRead (ThreadIndex);

            ReloadRoot ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NavigationController.ToolbarHidden = true;
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            var blurry = segue.DestinationViewController as BlurryViewController;
            if (null != blurry) {
                blurry.CaptureView (this.View);
            }

            if (segue.Identifier == "MessageToMessagePriority") {
                var vc = (MessagePriorityViewController)segue.DestinationViewController;
                vc.thread = messages.GetEmailThread (ThreadIndex);
                vc.SetOwner (this);
            }
            if (segue.Identifier == "MessageToMessageAction") {
                var vc = (MessageActionViewController)segue.DestinationViewController;
                vc.thread = messages.GetEmailThread (ThreadIndex);
                vc.SetOwner (this);
            }
            if (segue.Identifier == "MessageToCompose") {
                var vc = (ComposeViewController)segue.DestinationViewController;
                vc.Action = (NSString)sender;
                vc.ActionThread = messages.GetEmailThread (ThreadIndex);
                vc.SetOwner (this);
            }
        }

        public void DismissMessageViewController (INachoMessageController vc)
        {
            vc.SetOwner (null);
            vc.DismissViewController (false, new NSAction (delegate {
                NavigationController.PopViewControllerAnimated (true);
            }));
        }

        public void DeleteThisMessage()
        {
            var t = messages.GetEmailThread (ThreadIndex);
            var m = t.First ();
            BackEnd.Instance.DeleteEmailCmd (m.AccountId, m.Id);
            NavigationController.PopViewControllerAnimated (true);
        }

        protected void ReloadRoot ()
        {
            var root = new RootElement (null);
            root.UnevenRows = true;

            var t = messages.GetEmailThread (ThreadIndex);
            var m = t.First ();

            var topSection = new Section ();
            root.Add (topSection);

            if (null != m.From) {
                topSection.Add (new StringElement ("From: " + m.From));
            }
            if (null != m.Subject) {
                topSection.Add (new StringElement ("Subject: " + m.Subject));
            }

            if (null != m.To) {
                string[] toList = m.To.Split (new Char [] { ',' });
                foreach (var s in toList) {
                    topSection.Add (new StringElement ("To: " + s));
                }
            }
            if (null != m.DisplayTo) {
                string[] displayToList = m.DisplayTo.Split (new Char[] { ';' });
                foreach (var s in displayToList) {
                    topSection.Add (new StringElement ("Display To: " + s));
                }
            }
            if (null != m.Cc) {
                string[] CcList = m.Cc.Split (new Char [] { ',' });
                foreach (var s in CcList) {
                    topSection.Add (new StringElement ("Cc: " + s));
                }
            }

            var attachments = BackEnd.Instance.Db.Table<McAttachment> ().Where (a => a.EmailMessageId == m.Id).ToList ();

            if (0 < attachments.Count) {
                var attachmentSection = new Section ("Attachments");
                root.Add (attachmentSection);
                foreach (var a in attachments) {
                    StyledStringElement s;
                    if (a.IsInline) {
                        s = new StyledStringElement (a.DisplayName, "Is inline", UITableViewCellStyle.Subtitle);
                    } else if (a.IsDownloaded) {
                        s = new StyledStringElement (a.DisplayName, "Is downloaded", UITableViewCellStyle.Subtitle);
                        s.Tapped += delegate {
                            DisplayAttachment (a);
                        };
                    } else if (a.PercentDownloaded > 0) {
                        s = new StyledStringElement (a.DisplayName, "Downloading...", UITableViewCellStyle.Subtitle);
                    } else {
                        s = new StyledStringElement (a.DisplayName, "Is not downloaded", UITableViewCellStyle.Subtitle);
                        s.Tapped += delegate {
                            DownloadAttachment (a);
                        };
                    }
                    attachmentSection.Add (s);
                }
            }

            var bodySection = new Section ();
            root.Add (bodySection);

            var body = m.GetBody (BackEnd.Instance.Db);
            if (null != body) {
                var bodySource = new MemoryStream (Encoding.UTF8.GetBytes (body));
                var bodyParser = new MimeParser (bodySource, MimeFormat.Default);
                var message = bodyParser.ParseMessage ();
                PlatformHelpers.motd = message; // for cid handler
                MimeHelpers.DumpMessage (message, 0);
                RenderMessage (message, bodySection);
            }
          
            Root = root;

        }

        void RenderMessage (MimeMessage message, Section section)
        {
            RenderMimeEntity (message.Body, section);
        }

        void RenderMimeEntity (MimeEntity entity, Section section)
        {
            if (entity is MessagePart) {
                // This entity is an attached message/rfc822 mime part.
                var messagePart = (MessagePart)entity;
                // If you'd like to render this inline instead of treating
                // it as an attachment, you would just continue to recurse:
                RenderMessage (messagePart.Message, section);
                return;
            }
            if (entity is Multipart) {
                // This entity is a multipart container.
                var multipart = (Multipart)entity;

                if (multipart.ContentType.Matches ("multipart", "alternative")) {
                    RenderBestAlternative (multipart, section);
                    return;
                }

                foreach (var subpart in multipart) {
                    RenderMimeEntity (subpart, section);
                }
                return;
            }

            // Everything that isn't either a MessagePart or a Multipart is a MimePart
            var part = (MimePart)entity;

            // Don't render anything that is explicitly marked as an attachment.
//            if (part.IsAttachment)
//                return;

            if (part is TextPart) {
                // This is a mime part with textual content.
                var text = (TextPart)part;

                if (text.ContentType.Matches ("text", "html")) {
                    RenderHtml (text.Text, section);
                } else {
                    RenderText (text.Text, section);
                }
                return;
            }
            if (entity.ContentType.Matches ("image", "*")) {
                RenderImage (part, section);
                return;
            }

            if (entity.ContentType.Matches ("application", "ics")) {
                NachoCore.Utils.Log.Error ("Unhandled ics: {0}\n", part.ContentType);
                return;
            }
            if (entity.ContentType.Matches ("application", "octet-stream")) {
                NachoCore.Utils.Log.Error ("Unhandled octet-stream: {0}\n", part.ContentType);
                return;
            }

            NachoCore.Utils.Log.Error ("Unhandled Render: {0}\n", part.ContentType);
        }

        /// <summary>
        /// Renders the best alternative.
        /// http://en.wikipedia.org/wiki/MIME#Alternative
        /// </summary>
        void RenderBestAlternative (Multipart multipart, Section section)
        {
            var e = multipart.Last ();
            RenderMimeEntity (e, section);

        }

        void RenderHtml (string html, Section section)
        {
            Log.Info (Log.LOG_RENDER, "Html element string:\n{0}", html);

            int i = 0;

            var web = new UIWebView (UIScreen.MainScreen.Bounds) {
                BackgroundColor = UIColor.White,
                ScalesPageToFit = true,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleRightMargin,
            };
            web.ScrollView.PagingEnabled = false;
            web.ScrollView.ScrollEnabled = false;
            web.ScrollView.MultipleTouchEnabled = true;

            web.LoadStarted += delegate {
                // this is called several times
                if (i++ == 0) {
                    ;
                }
            };

            web.LoadFinished += delegate {
                if (--i == 0) {
                    // we stopped loading
                    web.StopLoading ();
                    System.Drawing.RectangleF frame = web.Frame;
                    frame.Height = 1;
                    web.Frame = frame;
                    frame.Size = web.SizeThatFits (new System.Drawing.SizeF (0f, 0f));
                    web.Frame = frame;
                    Log.Info ("web frame: {0}", web, frame);
//                    web.Dispose ();
                }
            };
            web.LoadError += (webview, args) => {
                // we stopped loading
                if (web != null) {
                    web.LoadHtmlString (String.Format ("<html><center><font size=+5 color='red'>{0}:<br>{1}</font></center></html>", "An error occurred:", args.Error.LocalizedDescription), null);
                }
            };
            web.ShouldStartLoad += delegate(UIWebView webView, NSUrlRequest request, UIWebViewNavigationType navigationType) {
                if (UIWebViewNavigationType.LinkClicked == navigationType) {
                    UIApplication.SharedApplication.OpenUrl (request.Url);
                    return false;
                }
                NachoCore.Utils.Log.Info ("Html element link: {0}", request.Url);
                return true;
            };

            web.LoadHtmlString (html, null);
            var e = new UIViewElement ("", web, true);
            NachoCore.Utils.Log.Info ("Add webview element: {0}", e);
            section.Add (e);
        }

        void RenderText (string text, Section section)
        {
            var e = new MultilineElement (text);
            NachoCore.Utils.Log.Info ("Add multiline element: {0}", e);
            section.Add (e);
        }

        void RenderImage (MimePart part, Section section)
        {
            var image = PlatformHelpers.RenderImage (part);
            var view = new UIImageView (image);
            var e = new UIViewElement ("", view, true);
            NachoCore.Utils.Log.Info ("Add image element: {0}", e);
            section.Add (e);
        }

        void DisplayAttachment (McAttachment attachment)
        {
            var path = Path.Combine (BackEnd.Instance.AttachmentsDir, attachment.LocalFileName);
            UIDocumentInteractionController Preview = UIDocumentInteractionController.FromUrl (NSUrl.FromFilename (path));
            Preview.Delegate = new DocumentInteractionControllerDelegate (this);
            Preview.PresentPreview (true);
        }

        void DownloadAttachment (McAttachment attachment)
        {
            if (!attachment.IsDownloaded && (attachment.PercentDownloaded == 0)) {
                var account = BackEnd.Instance.Db.Table<McAccount> ().First ();
                BackEnd.Instance.DnldAttCmd (account.Id, attachment.Id);
                ReloadRoot ();
            }
        }

        void MarkAsRead (int index)
        {
            var account = BackEnd.Instance.Db.Table<McAccount> ().First ();
            var thread = messages.GetEmailThread (index);
            var message = thread.First ();
            if (false == message.IsRead) {
                BackEnd.Instance.MarkEmailReadCmd (account.Id, message.Id);
            }
        }

        public class DocumentInteractionControllerDelegate : UIDocumentInteractionControllerDelegate
        {
            UIViewController viewC;

            public DocumentInteractionControllerDelegate (UIViewController controller)
            {
                viewC = controller;
            }

            public override UIViewController ViewControllerForPreview (UIDocumentInteractionController controller)
            {
                return viewC;
            }

            public override UIView ViewForPreview (UIDocumentInteractionController controller)
            {
                return viewC.View;
            }

            public override RectangleF RectangleForPreview (UIDocumentInteractionController controller)
            {
                return viewC.View.Frame;
            }
        }
    }
}
