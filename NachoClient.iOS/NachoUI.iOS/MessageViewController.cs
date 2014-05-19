// This file has been autogenerated from a class added in the UI designer.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MimeKit;
using DDay.iCal;
using DDay.iCal.Serialization;
using DDay.iCal.Serialization.iCalendar;
using MonoTouch.Dialog;

namespace NachoClient.iOS
{
    public partial class MessageViewController : UIViewController, INachoMessageEditorParent, INachoCalendarItemEditorParent
    {
        public McEmailMessageThread thread;
        protected UIView view;
        protected UIView attachmentsView;
        protected List<McAttachment> attachments;

        public MessageViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

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
                foldersButton,
            };

            clockButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageViewToMessagePriority", this);
            };
            foldersButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageViewToMessageAction", this);
            };
            replyButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageViewToComposeView", ComposeViewController.Reply);
            };
            replyAllButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageViewToComposeView", ComposeViewController.ReplyAll);
            };
            forwardButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageViewToComposeView", ComposeViewController.Forward);
            };
            deleteButton.Clicked += (object sender, EventArgs e) => {
                DeleteThisMessage ();
                NavigationController.PopViewControllerAnimated (true);
            };
            checkButton.Clicked += (object sender, EventArgs e) => {
                ArchiveThisMessage ();
                NavigationController.PopViewControllerAnimated (true);
            };

            // Watch for changes from the back end
            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind) {
                    RefreshAttachmentSection ();
                }
            };

            MarkAsRead ();

        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = false;
            }
            CreateView ();
            MyLayout ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            var blurry = segue.DestinationViewController as BlurryViewController;
            if (null != blurry) {
                blurry.CaptureView (this.View);
            }

            if (segue.Identifier == "MessageViewToMessagePriority") {
                var vc = (MessagePriorityViewController)segue.DestinationViewController;
                vc.thread = thread;
                vc.SetOwner (this);
            }
            if (segue.Identifier == "MessageViewToMessageAction") {
                var vc = (MessageActionViewController)segue.DestinationViewController;
                vc.thread = thread;
                vc.SetOwner (this);
            }
            if (segue.Identifier == "MessageViewToComposeView") {
                var vc = (ComposeViewController)segue.DestinationViewController;
                vc.Action = (NSString)sender;
                vc.ActionThread = thread;
                vc.SetOwner (this);
            }
            if (segue.Identifier == "MessageViewToCalendarItemEdit") {
                var vc = (CalendarItemViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                var c = h.value as McCalendar;
                vc.SetOwner (this);
                vc.SetCalendarItem (c, CalendarItemEditorAction.edit);
            }
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                NavigationController.PopViewControllerAnimated (true);
            }));
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var m = thread.SingleMessageSpecialCase ();
            var t = CalendarHelper.CreateTask (m);
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                PerformSegue ("", new SegueHolder (t));
            }));
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var m = thread.SingleMessageSpecialCase ();
            var c = CalendarHelper.CreateMeeting (m);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                PerformSegue ("MessageViewToCalendarItemEdit", new SegueHolder (c));
            }));
        }

        public void DismissChildCalendarItemEditor (INachoCalendarItemEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissCalendarItemEditor (true, null);
        }
       
        void MarkAsRead ()
        {
            var account = NcModel.Instance.Db.Table<McAccount> ().First ();
            var message = thread.SingleMessageSpecialCase ();
            if (false == message.IsRead) {
                BackEnd.Instance.MarkEmailReadCmd (account.Id, message.Id);
            }
        }

        public void DeleteThisMessage ()
        {
            var m = thread.SingleMessageSpecialCase ();
            NcEmailArchiver.Delete (m);
        }

        public void ArchiveThisMessage ()
        {
            var m = thread.SingleMessageSpecialCase ();
            NcEmailArchiver.Archive (m);
        }

        protected void CreateView ()
        {
            var m = thread.SingleMessageSpecialCase ();

            attachments = McAttachment.QueryByItemId<McEmailMessage> (m.AccountId, m.Id);

            // Start fresh
            var subviews = scrollView.Subviews;
            foreach (var s in subviews) {
                s.RemoveFromSuperview ();
            }

            view = new UIView ();
            scrollView.AddSubview (view);

            scrollView.DidZoom += (object sender, EventArgs e) => {
                Log.Info ("scrollview did zoom");
            };
            scrollView.MinimumZoomScale = 0.3f;
            scrollView.MaximumZoomScale = 4.0f;
            scrollView.ViewForZoomingInScrollView = delegate {
                return view;
            };

            var doubletap = new UITapGestureRecognizer ();
            doubletap.NumberOfTapsRequired = 2;
            doubletap.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("DoubleTapSelector:"));
            doubletap.ShouldRecognizeSimultaneously = delegate {
                return true;
            };
            scrollView.AddGestureRecognizer (doubletap);

            if (null != m.Subject) {
                var subject = new UILabel (new RectangleF (0.0f, 0.0f, 320.0f, 1.0f));
                subject.Text = m.Subject;
                subject.Font = UIFont.BoldSystemFontOfSize (19.0f);
                subject.Lines = 0;
                subject.LineBreakMode = UILineBreakMode.WordWrap;
                subject.SizeToFit ();
                view.AddSubview (subject);
            }

            var bodyPath = m.GetBodyPath ();
            if (null != bodyPath) {
                using (var bodySource = new FileStream (bodyPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    var bodyParser = new MimeParser (bodySource, MimeFormat.Default);
                    var message = bodyParser.ParseMessage ();
                    PlatformHelpers.motd = message; // for cid handler
                    MimeHelpers.DumpMessage (message, 0);
                    var list = new List<MimeEntity> ();
                    MimeHelpers.MimeDisplayList (message, ref list);
                    RenderDisplayList (list);
                }
            }

            CreateAttachmentSection ();

        }

        [MonoTouch.Foundation.Export ("DoubleTapSelector:")]
        public void OnDoubleTap (UIGestureRecognizer sender)
        {
            if (scrollView.ZoomScale == 1.0f) {
                scrollView.SetZoomScale (2.0f, true);
            } else {
                scrollView.SetZoomScale (1.0f, true);
            }
            MyLayout ();
        }

        protected void MyLayout ()
        {
            float x = 320.0f;
            float y = 0.0f;
            for (int i = 0; i < view.Subviews.Count (); i++) {
                var v = view.Subviews [i];
                var f = v.Frame;
                f.Y = y;
                v.Frame = f;
                y += f.Height;
                if (f.Width > x) {
                    x = f.Width;
                }
            }

            view.Frame = new RectangleF (0.0f, 0.0f, x, y);
            scrollView.ContentSize = new SizeF (x, y);
            scrollView.SetNeedsDisplay ();
        }

        protected void CreateAttachmentSection ()
        {
            if (0 == attachments.Count) {
                return;
            }

            var root = new RootElement ("");
            var section = new ThinSection ();
            root.Add (section);

            foreach (var a in attachments) {
                StyledStringElement s;
                if (a.IsInline) {
                    s = new StyledStringElement (a.DisplayName, "Is inline", UITableViewCellStyle.Subtitle);
                } else if (a.IsDownloaded) {
                    s = new StyledStringElement (a.DisplayName, "Is downloaded", UITableViewCellStyle.Subtitle);
                    s.Tapped += delegate {
                        var id = a.Id;
                        attachmentAction (id);
                    };
                } else if (a.PercentDownloaded > 0) {
                    s = new StyledStringElement (a.DisplayName, "Downloading...", UITableViewCellStyle.Subtitle);
                } else {
                    s = new StyledStringElement (a.DisplayName, "Is not downloaded", UITableViewCellStyle.Subtitle);
                    s.Tapped += delegate {
                        var id = a.Id;
                        attachmentAction (id);
                    };
                }
                section.Add (s);
            }

            var dvc = new DialogViewController (root);
            attachmentsView = dvc.View;
            view.AddSubview (attachmentsView);
        }
        // TOOD: Verify cells are in attachment order
        protected void RefreshAttachmentSection ()
        {
            // Use 'this' to get non-null from delegate callback
            if (null == this.attachmentsView) {
                return;
            }
            var tv = this.attachmentsView as UITableView;
            if (null == tv) {
                Log.Error ("expected UITableView in RefreshAttachmentSection");
                return;
            }

            var m = thread.SingleMessageSpecialCase ();
            attachments = McAttachment.QueryByItemId<McEmailMessage> (m.AccountId, m.Id);

            for (int i = 0; i < attachments.Count; i++) {
                var a = attachments [i];
                var c = tv.VisibleCells [i];
                NachoAssert.True (null != a);
                NachoAssert.True (null != c);
                NachoAssert.True (a.DisplayName.Equals (c.TextLabel.Text));
                if (a.IsDownloaded) {
                    c.DetailTextLabel.Text = "Is downloaded";
                } else if (a.PercentDownloaded > 0) {
                    c.DetailTextLabel.Text = "Downloading...";
                } else {
                    c.DetailTextLabel.Text = "Is not downloaded";
                }
            }
        }

        protected void RenderDisplayList (List<MimeEntity> list)
        {
            for (var i = 0; i < list.Count; i++) {
                var entity = list [i];
                var part = (MimePart)entity;
                if (part.ContentType.Matches ("text", "html")) {
                    RenderHtml (part);
                    continue;
                }
                if (part.ContentType.Matches ("text", "calendar")) {
                    RenderCalendar (part);
                    continue;
                }
                if (part.ContentType.Matches ("text", "*")) {
                    RenderText (part);
                    continue;
                }
                if (part.ContentType.Matches ("image", "*")) {
                    RenderImage (part);
                    continue;
                }
            }
        }

        void RenderText (MimePart part)
        {
            var textPart = part as TextPart;
            var text = textPart.Text;
            var label = new UILabel (new RectangleF (0.0f, 0.0f, 320.0f, 1.0f));
            label.Lines = 0;
            label.Font = UIFont.SystemFontOfSize (17.0f);
            label.LineBreakMode = UILineBreakMode.WordWrap;
            label.Text = text;
            label.SizeToFit ();
            view.AddSubview (label);
        }

        void RenderImage (MimePart part)
        {
            var image = PlatformHelpers.RenderImage (part);

            // FIXME: Hard-coded width
            float width = 320.0f;
            float height = image.Size.Height * (width / image.Size.Width);
            image = image.Scale (new SizeF (width, height));

            var iv = new UIImageView (image);
            view.AddSubview (iv);

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

        void RenderHtml (MimePart part)
        {
            var textPart = part as TextPart;
            var html = textPart.Text;

            Log.Info (Log.LOG_RENDER, "Html element string:\n{0}", html);

            int i = 0;

            var web = new UIWebView (UIScreen.MainScreen.Bounds);
            web.BackgroundColor = UIColor.White;
            web.ContentMode = UIViewContentMode.ScaleAspectFit;
            web.ScrollView.PagingEnabled = false;
            web.ScrollView.ScrollEnabled = true;
            web.ScrollView.MultipleTouchEnabled = false;

            view.AddSubview (web);

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
                    System.Drawing.RectangleF frame = web.Frame;
                    frame.Height = 1;
                    frame.Width = 320;
                    web.Frame = frame;
                    frame.Height = web.ScrollView.ContentSize.Height > View.Bounds.Height ? View.Bounds.Height : web.ScrollView.ContentSize.Height;
                    Log.Info ("frame = {0}", frame);

                    web.Frame = frame;
                    web.Alpha = 1.0f;
                    view.Frame = frame;
                    MyLayout ();
                    Log.Info ("content size = {0}", scrollView.ContentSize);

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

        }
        // Gets the decoded text content.
        public string GetText (TextPart text)
        {
            return text.Text;
        }

        /// TODO: Guard against malformed calendars
        public void RenderCalendar (MimePart part)
        {
            var textPart = part as TextPart;
            var decodedText = GetText (textPart);
            var stringReader = new StringReader (decodedText);
            IICalendar iCal = iCalendar.LoadFromStream (stringReader) [0];
            var evt = iCal.Events.First () as DDay.iCal.Event;
            NachoCore.Utils.CalendarHelper.ExtrapolateTimes (ref evt);

            var root = new RootElement ("");
            var section = new ThinSection ();
            root.Add (section);

            if (null != evt.Summary) {
                section.Add (new SubjectElement (evt.Summary));
            }
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
                UpdateMeetingStatus (evt, NcAttendeeStatus.Accept);
            };
            var button2 = new StyledStringElementWithDot ("Tentative", UIColor.Yellow);
            button2.Tapped += () => {
                UpdateMeetingStatus (evt, NcAttendeeStatus.Tentative);
            };
            var button3 = new StyledStringElementWithDot ("Decline", UIColor.Red);
            button3.Tapped += () => {
                UpdateMeetingStatus (evt, NcAttendeeStatus.Decline);
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

            var dvc = new DialogViewController (root);
            view.AddSubview (dvc.View);
        }

        /// <summary>
        /// Map meeting uid to calendar record.
        /// </summary>
        void UpdateMeetingStatus (IEvent evt, NcAttendeeStatus status)
        {
            // TODO: Map meeting uid to calendar record; update status
        }

        void attachmentAction (int attachmentId)
        {
            var a = McAttachment.QueryById<McAttachment> (attachmentId);
            if (a.IsDownloaded) {
                PlatformHelpers.DisplayAttachment (this, a);
            } else {
                PlatformHelpers.DownloadAttachment (a);
            }
        }

    }
}
