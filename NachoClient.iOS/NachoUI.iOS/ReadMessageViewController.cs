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
using MimeKit.Utils;
using MimeKit.Encodings;
using DDay.iCal;
using DDay.iCal.Serialization;
using DDay.iCal.Serialization.iCalendar;

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
                DeleteThisMessage ();
            };
            // Watch for changes from the back end
            BackEnd.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind) {
                    RefreshAttachments ();
                }
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

        public void DeleteThisMessage ()
        {
            var t = messages.GetEmailThread (ThreadIndex);
            var m = t.First ();
            BackEnd.Instance.DeleteEmailCmd (m.AccountId, m.Id);
            NavigationController.PopViewControllerAnimated (true);
        }

        Section attachmentSection;

        protected void RefreshAttachments ()
        {
            UpdateAttachmentSection ();
            if (null != attachmentSection) {
                attachmentSection.GetImmediateRootElement ().Reload (attachmentSection, UITableViewRowAnimation.None);
            }
        }

        protected void UpdateAttachmentSection ()
        {
            var t = messages.GetEmailThread (ThreadIndex);
            var m = t.First ();
            var attachments = BackEnd.Instance.Db.Table<McAttachment> ().Where (a => a.EmailMessageId == m.Id).ToList ();

            if (0 == attachments.Count) {
                attachmentSection = null;
                return;
            }

            if (null == attachmentSection) {
                attachmentSection = new ThinSection ();
            } else {
                attachmentSection.Clear ();
            }

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

        protected void ReloadRoot ()
        {
            var t = messages.GetEmailThread (ThreadIndex);
            var m = t.First ();

            var root = new RootElement ("");
            root.UnevenRows = true;

            var topSection = new ThinSection ();

            root.Add (topSection);

            if (null != m.Subject) {
                topSection.Add (new MultilineElement (m.Subject));
            }

            topSection.Add (new StartTimeElement (Pretty.FullDateString (m.DateReceived)));

            attachmentSection = null;
            UpdateAttachmentSection ();
            if (null != attachmentSection) {
                root.Add (attachmentSection);
            }
           
            var bodySection = new ThinSection ();
            root.Add (bodySection);

            var body = m.GetBody ();
            if (null != body) {
                var bodySource = new MemoryStream (Encoding.UTF8.GetBytes (body));
                var bodyParser = new MimeParser (bodySource, MimeFormat.Default);
                var message = bodyParser.ParseMessage ();
                PlatformHelpers.motd = message; // for cid handler
                MimeHelpers.DumpMessage (message, 0);
                var list = new List<MimeEntity> ();
                MimeHelpers.MimeDisplayList (message, ref list);
                RenderDisplayList (list, bodySection);
            }
          
            Root = root;

        }

        protected void RenderDisplayList (List<MimeEntity> list, Section section)
        {
            for (var i = 0; i < list.Count; i++) {
                var entity = list [i];
                var part = (MimePart)entity;
                if (part.ContentType.Matches ("text", "html")) {
                    RenderHtml (part, section);
                    continue;
                }
                if (part.ContentType.Matches ("text", "calendar")) {
                    RenderCalendar (part, section);
                    continue;
                }
                if (part.ContentType.Matches ("text", "*")) {
                    RenderText (part, section);
                    continue;
                }
                if (part.ContentType.Matches ("image", "*")) {
                    RenderImage (part, section);
                    continue;
                }
            }
        }

        string magic = @"
            var style = document.createElement(""style""); 
            document.head.appendChild(style); 
            style.innerHTML = ""html{-webkit-text-size-adjust: auto;}"";
            var viewPortTag=document.createElement('meta');
            viewPortTag.id=""viewport"";
            viewPortTag.name = ""viewport"";
            viewPortTag.content = ""width=device-width; initial-scale=1.0;"";
            document.getElementsByTagName('head')[0].appendChild(viewPortTag);
        ";


        void RenderHtml (MimePart part, Section section)
        {
            var textPart = part as TextPart;
            var html = textPart.Text;

            Log.Info (Log.LOG_RENDER, "Html element string:\n{0}", html);

            int i = 0;

            var web = new UIWebView (UIScreen.MainScreen.Bounds) {
                BackgroundColor = UIColor.White,
                ScalesPageToFit = true,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleRightMargin
            };
            web.ContentMode = UIViewContentMode.ScaleAspectFit;
            web.ScrollView.PagingEnabled = false;
            web.ScrollView.ScrollEnabled = false;
            web.ScrollView.MultipleTouchEnabled = true;

            var e = new UIViewElement ("", web, true);
            section.Add (e);
            NachoCore.Utils.Log.Info ("Add webview element: {0}", e);

            web.LoadHtmlString (html, null);
            web.Alpha = 0.0f;

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
                    // Size viewport and text
                    web.EvaluateJavascript (magic);
//                    string padding = "document.body.style.margin='0';document.body.style.padding = '0'";
//                    web.EvaluateJavascript (padding);
                    System.Drawing.RectangleF frame = web.Frame;
                    frame.Height = 1;
                    frame.Width = 320;
                    web.Frame = frame;
                    frame.Height = web.ScrollView.ContentSize.Height;
                    web.Frame = frame;
                    e.GetActiveCell().Frame = frame;
                    UIView.BeginAnimations(null);
                    UIView.SetAnimationDuration(0.30);
                    web.Alpha = 1.0f;
                    UIView.CommitAnimations();
                    e.GetImmediateRootElement().Reload(e, UITableViewRowAnimation.None);
                }
            };
            web.ScrollView.ViewForZoomingInScrollView = delegate {
                return web;
            };
            web.ScrollView.DidZoom += (object sender, EventArgs ee) => {
                System.Drawing.RectangleF frame = web.Frame;
                frame.Height = web.ScrollView.ContentSize.Height;
                web.Frame = frame;
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

        }

        void RenderText (MimePart part, Section section)
        {
            var textPart = part as TextPart;
            var text = textPart.Text;
            var e = new StyledMultilineElement (text);
            e.Font = UIFont.SystemFontOfSize (19.0f);
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
                // FIXME BackEnd.Instance.MarkEmailReadCmd (account.Id, message.Id);
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
        // Gets the decoded text content.
        public string GetText (TextPart text)
        {
            return text.Text;
        }
        // TODO: Malformed calendars
        public void RenderCalendar (MimePart part, Section section)
        {
            var textPart = part as TextPart;
            var decodedText = GetText (textPart);
            var stringReader = new StringReader (decodedText);
            IICalendar iCal = iCalendar.LoadFromStream (stringReader) [0];
            IEvent evt = iCal.Events.First ();

            section.Add (new SubjectElement (evt.Summary));
            section.Add (new StartTimeElement (Pretty.FullDateString (evt.Start.Value)));
            if (evt.IsAllDay) {
                section.Add (new DurationElement (Pretty.AllDayStartToEnd (evt.Start.Value, evt.End.Value)));
            } else {
                section.Add (new DurationElement (Pretty.EventStartToEnd (evt.Start.Value, evt.End.Value)));
            }

            if (null != evt.Location) {
                section = new ThinSection ();
                section.Add (new LocationElement (evt.Location));
            }

            var button1 = new StyledStringElementWithDot ("Accept", UIColor.Green);
            button1.Tapped += () => {
                UpdateStatus (evt, NcAttendeeStatus.Accept);
            };
            var button2 = new StyledStringElementWithDot ("Tentative", UIColor.Yellow);
            button2.Tapped += () => {
                UpdateStatus (evt, NcAttendeeStatus.Tentative);
            };
            var button3 = new StyledStringElementWithDot ("Decline", UIColor.Red);
            button3.Tapped += () => {
                UpdateStatus (evt, NcAttendeeStatus.Decline);
            };
            section.Add (button1);
            section.Add (button2);
            section.Add (button3);

            {
                var e = new StyledStringElement ("People");
                var image = UIImage.FromBundle ("ic_action_group");
                e.Image = image.Scale (new SizeF (22.0f, 22.0f));
                e.Font = UIFont.SystemFontOfSize (17.0f);
//                e.Tapped += () => {
//                    PushAttendeeView ();
//                };
                e.Accessory = UITableViewCellAccessory.DisclosureIndicator;
            }

//            section = new ThinSection ();
//            using (var image = UIImage.FromBundle ("ic_action_alarms")) {
//                var scaledImage = image.Scale (new SizeF (22.0f, 22.0f));
//                section.Add (new StyledStringElementWithIcon ("Reminder", Pretty.ReminderString (evt.Alarms.First()), scaledImage));
//            }
//            root.Add (section);

        }

        void UpdateStatus (IEvent evt, NcAttendeeStatus status)
        {
        }
    }
}
