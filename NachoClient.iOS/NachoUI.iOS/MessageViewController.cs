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
    public partial class MessageViewController : NcUIViewController, INachoMessageEditorParent, INachoFolderChooserParent, INachoCalendarItemEditorParent
    {
        public McEmailMessageThread thread;
        protected UIView view;
        protected UIView attachmentListView;
        protected List<McAttachment> attachments;

        protected int htmlBusy;
        protected int deferLayout;
        protected object deferLayoutLock = new object ();

        public MessageViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Multiple buttons spaced evently
            ToolbarItems = new UIBarButtonItem[] {
                replyButton,
                flexibleSpaceButton,
                archiveButton,
                fixedSpaceButton,
                saveButton,
                fixedSpaceButton,
                deleteButton
            };

            // Multiple buttons on the right side
            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                deferButton,
                quickReplyButton
            };

            deferButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageViewToMessagePriority", this);
            };
            saveButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageViewToMessageAction", this);
            };
            replyButton.Clicked += (object sender, EventArgs e) => {
                ReplyActionSheet ();
            };
            archiveButton.Clicked += (object sender, EventArgs e) => {
                ArchiveThisMessage ();
                NavigationController.PopViewControllerAnimated (true);
            };
            deleteButton.Clicked += (object sender, EventArgs e) => {
                DeleteThisMessage ();
                NavigationController.PopViewControllerAnimated (true);
            };

            FetchAttachments ();
            CreateView ();

            MarkAsRead ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = false;
            }
            ConfigureView ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if ((NcResult.SubKindEnum.Info_AttDownloadUpdate == s.Status.SubKind) || (NcResult.SubKindEnum.Error_AttDownloadFailed == s.Status.SubKind)) {
                FetchAttachments ();
                ConfigureAttachments ();
            }
        }

        protected void FetchAttachments ()
        {
            var message = thread.SingleMessageSpecialCase ();
            attachments = McAttachment.QueryByItemId<McEmailMessage> (message.AccountId, message.Id);
        }

        protected void ReplyActionSheet ()
        {
            var actionSheet = new UIActionSheet ();
            actionSheet.Add ("Reply");
            actionSheet.Add ("Reply All");
            actionSheet.Add ("Forward");
            actionSheet.Add ("Cancel");

            actionSheet.CancelButtonIndex = 3;

            actionSheet.Clicked += delegate(object a, UIButtonEventArgs b) {
                switch (b.ButtonIndex) {
                case 0:
                    PerformSegue ("MessageViewToCompose", new SegueHolder (MessageComposeViewController.Reply));
                    break;
                case 1:
                    PerformSegue ("MessageViewToCompose", new SegueHolder (MessageComposeViewController.ReplyAll));
                    break;
                case 2:
                    PerformSegue ("MessageViewToCompose", new SegueHolder (MessageComposeViewController.Forward));
                    break;
                case 3:
                    break; // Cancel
                }
            };
            actionSheet.ShowFromToolbar (NavigationController.Toolbar);
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
                vc.SetOwner (this, thread);
            }
            if (segue.Identifier == "MessageViewToCompose") {
                var vc = (MessageComposeViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                vc.Action = (string)h.value;
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

        /// <summary>
        /// INachoFolderChooser delegate
        /// </summary>
        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            MoveThisMessage (folder);
            vc.SetOwner (null, null);
            vc.DismissFolderChooser (false, new NSAction (delegate {
                NavigationController.PopViewControllerAnimated (true);
            }));
        }

        /// <summary>
        /// INachoFolderChooser delegate
        /// </summary>
        public void DismissChildFolderChooser (INachoFolderChooser vc)
        {
            vc.DismissFolderChooser (true, null);
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

        public void MoveThisMessage (McFolder folder)
        {
            var m = thread.SingleMessageSpecialCase ();
            NcEmailArchiver.Move (m, folder);
        }

        const int USER_IMAGE_TAG = 101;
        const int FROM_TAG = 102;
        const int SUBJECT_TAG = 103;
        const int REMINDER_TEXT_TAG = 104;
        const int REMINDER_ICON_TAG = 105;
        const int ATTACHMENT_ICON_TAG = 106;
        const int RECEIVED_DATE_TAG = 107;
        const int SEPARATOR_TAG = 108;
        const int MESSAGE_PART_TAG = 300;
        const int ATTACHMENT_VIEW_TAG = 301;
        const int ATTACHMENT_NAME_TAG = 302;
        const int ATTACHMENT_STATUS_TAG = 303;

        protected void CreateView ()
        {
            view = new UIView ();
            scrollView.AddSubview (view);

            scrollView.DidZoom += (object sender, EventArgs e) => {
                Log.Info (Log.LOG_UI, "scrollview did zoom");
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

            // User image view
            var userImageView = new UIImageView (new RectangleF (15, 15, 40, 40));
            userImageView.Layer.CornerRadius = 20;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Tag = USER_IMAGE_TAG;
            view.AddSubview (userImageView);

            // From label view
            // Font will vary bold or regular, depending on isRead.
            // Size fields will be recalculated after text is known.
            var fromLabelView = new UILabel (new RectangleF (65, 20, 150, 20));
            fromLabelView.Font = A.Font_AvenirNextDemiBold17;
            fromLabelView.TextColor = A.Color_0F424C;
            fromLabelView.Tag = FROM_TAG;
            view.AddSubview (fromLabelView);

            // Subject label view
            // Size fields will be recalculated after text is known.
            var subjectLabelView = new UILabel (new RectangleF (65, 40, 250, 20));
            subjectLabelView.LineBreakMode = UILineBreakMode.TailTruncation;
            subjectLabelView.Font = A.Font_AvenirNextMedium14;
            subjectLabelView.TextColor = A.Color_0F424C;
            subjectLabelView.Tag = SUBJECT_TAG;
            view.AddSubview (subjectLabelView);

            // Reminder image view
            var reminderImageView = new UIImageView (new RectangleF (65, 64, 12, 12));
            reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
            reminderImageView.Tag = REMINDER_ICON_TAG;
            view.AddSubview (reminderImageView);

            // Reminder label view
            var reminderLabelView = new UILabel (new RectangleF (87, 60, 230, 20));
            reminderLabelView.Font = A.Font_AvenirNextRegular14;
            reminderLabelView.TextColor = A.Color_9B9B9B;
            reminderLabelView.Tag = REMINDER_TEXT_TAG;
            view.AddSubview (reminderLabelView);

            // Attachment image view
            // Attachment 'x' will be adjusted to be left of date received field
            var attachmentImageView = new UIImageView (new RectangleF (200, 18, 16, 16));
            attachmentImageView.Image = UIImage.FromBundle ("inbox-icn-attachment");
            attachmentImageView.Tag = ATTACHMENT_ICON_TAG;
            view.AddSubview (attachmentImageView);

            var tapAttachmentIconGestureRecognizer = new UITapGestureRecognizer ((UITapGestureRecognizer obj) => {
                onAttachmentIconSelected (obj);
            });
            tapAttachmentIconGestureRecognizer.Enabled = true;
            attachmentImageView.UserInteractionEnabled = true;
            attachmentImageView.AddGestureRecognizer (tapAttachmentIconGestureRecognizer);

            // Received label view
            var receivedLabelView = new UILabel (new RectangleF (220, 18, 100, 20));
            receivedLabelView.Font = A.Font_AvenirNextRegular14;
            receivedLabelView.TextColor = A.Color_9B9B9B;
            receivedLabelView.TextAlignment = UITextAlignment.Right;
            receivedLabelView.Tag = RECEIVED_DATE_TAG;
            view.AddSubview (receivedLabelView);

            // Separator
            var separatorView = new UIView (new RectangleF (0, 80, 320, 1));
            separatorView.BackgroundColor = A.Color_NachoNowBackground;
            separatorView.Tag = SEPARATOR_TAG;
            view.AddSubview (separatorView);

            // Attachments

            attachmentListView = new UIView ();
            attachmentListView.Tag = ATTACHMENT_VIEW_TAG;

            for (int i = 0; i < attachments.Count; i++) {
                var attachmentView = new UIView (new RectangleF (0, i * 61, View.Frame.Width, 61));
                attachmentView.Layer.BorderColor = A.Color_NachoNowBackground.CGColor;
                attachmentView.Layer.BorderWidth = 1;
                attachmentView.Tag = i;
                attachmentListView.AddSubview (attachmentView);

                var icon = new UIImageView (new RectangleF (15, 22, 16, 16));
                icon.Image = UIImage.FromBundle ("icn-attach-files");
                attachmentView.AddSubview (icon);

                var name = new UILabel (new RectangleF (49, 10, View.Frame.Width, 20));
                name.Font = A.Font_AvenirNextMedium14;
                name.TextColor = A.Color_808080;
                name.Tag = ATTACHMENT_NAME_TAG;
                attachmentView.AddSubview (name);

                var status = new UILabel (new RectangleF (49, 30, View.Frame.Width, 20));
                status.Font = A.Font_AvenirNextMedium14;
                status.TextColor = A.Color_808080;
                status.Tag = ATTACHMENT_STATUS_TAG;
                attachmentView.AddSubview (status);

                // Tap the calendar thumb to hid the calendar again
                var tapGestureRecognizer = new UITapGestureRecognizer ((UITapGestureRecognizer obj) => {
                    onAttachmentSelected (obj);
                });
                tapGestureRecognizer.Enabled = true;
                attachmentView.AddGestureRecognizer (tapGestureRecognizer);
            }
            attachmentListView.Frame = new RectangleF (0, 0, View.Frame.Width, 61 * attachments.Count);
        }

        protected void ConfigureView ()
        {
            var message = thread.SingleMessageSpecialCase ();
            attachments = McAttachment.QueryByItemId<McEmailMessage> (message.AccountId, message.Id);

            var userImageView = View.ViewWithTag (USER_IMAGE_TAG) as UIImageView;
            var emailOfSender = Pretty.EmailString (message.From);
            string sender = Pretty.SenderString (message.From);

            int circleColorNum = Util.SenderToCircle (message.AccountId, emailOfSender);
            UIColor circleColor = Util.IntToUIColor (circleColorNum);
            userImageView.Image = Util.LettersWithColor (sender, circleColor, A.Font_AvenirNextUltraLight24);

            // Subject label view
            var subjectLabelView = View.ViewWithTag (SUBJECT_TAG) as UILabel;
            subjectLabelView.Text = Pretty.SubjectString (message.Subject);

            // Reminder image view and label
            var ySeparator = 75;
            var reminderImageView = View.ViewWithTag (REMINDER_ICON_TAG) as UIImageView;
            var reminderLabelView = View.ViewWithTag (REMINDER_TEXT_TAG) as UILabel;
            if (message.HasDueDate () || message.IsDeferred ()) {
                ySeparator = 95;
                reminderImageView.Hidden = false;
                reminderLabelView.Hidden = false;
                if (message.IsDeferred ()) {
                    reminderLabelView.Text = String.Format ("Message hidden until {0}", message.FlagDueAsUtc ());
                } else if (message.IsOverdue ()) {
                    reminderLabelView.Text = String.Format ("Response was due {0}", message.FlagDueAsUtc ());
                } else {
                    reminderLabelView.Text = String.Format ("Response is due {0}", message.FlagDueAsUtc ());
                }
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
            }
            var separatorView = View.ViewWithTag (SEPARATOR_TAG);
            separatorView.Frame = new RectangleF (0, ySeparator, View.Frame.Width, 1);

            // Received label view
            var receivedLabelView = View.ViewWithTag (RECEIVED_DATE_TAG) as UILabel;
            receivedLabelView.Text = Pretty.CompactDateString (message.DateReceived);
            receivedLabelView.SizeToFit ();
            var receivedLabelRect = receivedLabelView.Frame;
            receivedLabelRect.X = View.Frame.Width - 15 - receivedLabelRect.Width;
            receivedLabelRect.Height = 20;
            receivedLabelView.Frame = receivedLabelRect;

            // Attachment image view
            var attachmentImageView = View.ViewWithTag (ATTACHMENT_ICON_TAG) as UIImageView;
            attachmentImageView.Hidden = (0 == attachments.Count);
            var attachmentImageRect = attachmentImageView.Frame;
            attachmentImageRect.X = receivedLabelRect.X - 10 - 16;
            attachmentImageView.Frame = attachmentImageRect;

            // From label view
            var fromLabelView = View.ViewWithTag (FROM_TAG) as UILabel;
            var fromLabelRect = fromLabelView.Frame;
            fromLabelRect.Width = attachmentImageRect.X - 65;
            fromLabelView.Frame = fromLabelRect;
            fromLabelView.Text = Pretty.SenderString (message.From);
            fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextDemiBold17 : A.Font_AvenirNextRegular17);

            htmlBusy = 0;
            deferLayout = 1;

            // TODO: Revisit
            for (int i = 0; i < view.Subviews.Count (); i++) {
                var v = view.Subviews [i];
                if (MESSAGE_PART_TAG == v.Tag) {
                    v.RemoveFromSuperview ();
                }
            }

            var bodyPath = message.GetBodyPath ();
            if (null != bodyPath) {
                using (var bodySource = new FileStream (bodyPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    var bodyParser = new MimeParser (bodySource, MimeFormat.Default);
                    var mime = bodyParser.ParseMessage ();
                    PlatformHelpers.motd = mime; // for cid handler
                    MimeHelpers.DumpMessage (mime, 0);
                    var list = new List<MimeEntity> ();
                    MimeHelpers.MimeDisplayList (mime, ref list);
                    RenderDisplayList (list);
                }
            }

            ConfigureAttachments ();

            if (0 == DeferLayoutDecrement ()) {
                LayoutView ();
            }
        }

        protected void ConfigureAttachments ()
        {
            for (int i = 0; i < attachments.Count; i++) {
                var attachment = attachments [i];
                var attachmentView = attachmentListView.ViewWithTag (i);
                var name = attachmentView.ViewWithTag (ATTACHMENT_NAME_TAG) as UILabel;
                name.Text = attachment.DisplayName;
                var status = attachmentView.ViewWithTag (ATTACHMENT_STATUS_TAG) as UILabel;
                if (attachment.IsInline) {
                    status.Text = "Is an inline attachment.";
                } else if (attachment.IsDownloaded) {
                    status.Text = "Attachment is downloaded.";
                } else if (0 < attachment.PercentDownloaded) {
                    status.Text = "Attachment is downloading.";
                } else {
                    status.Text = "Touch to download attachment.";
                }
                attachmentView.SetNeedsDisplay ();
            }
        }

        protected void LayoutView ()
        {
            var maxWidth = View.Frame.Width;

            var separatorView = view.ViewWithTag (SEPARATOR_TAG);
            var yOffset = separatorView.Frame.Y + separatorView.Frame.Height;

            yOffset += 15;

            attachmentListView.RemoveFromSuperview ();
            view.AddSubview (attachmentListView);

            for (int i = 0; i < view.Subviews.Count (); i++) {
                var v = view.Subviews [i];
                if ((MESSAGE_PART_TAG == v.Tag) || (ATTACHMENT_VIEW_TAG == v.Tag)) {
                    var frame = v.Frame;
                    frame.Y = yOffset;
                    v.Frame = frame;
                    yOffset += frame.Height;
                    if (frame.Width > maxWidth) {
                        maxWidth = frame.Width;
                    }
                }
            }
            view.Frame = new RectangleF (0.0f, 0.0f, maxWidth, yOffset);
            scrollView.ContentSize = new SizeF (maxWidth, yOffset);
            scrollView.SetNeedsDisplay ();
        }

        [MonoTouch.Foundation.Export ("DoubleTapSelector:")]
        public void OnDoubleTap (UIGestureRecognizer sender)
        {
            if (scrollView.ZoomScale == 1.0f) {
                scrollView.SetZoomScale (2.0f, true);
            } else {
                scrollView.SetZoomScale (1.0f, true);
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
            label.Tag = MESSAGE_PART_TAG;
            view.AddSubview (label);
        }

        void RenderImage (MimePart part)
        {
            var image = PlatformHelpers.RenderImage (part);

            float width = View.Frame.Width;
            float height = image.Size.Height * (width / image.Size.Width);
            image = image.Scale (new SizeF (width, height));

            var iv = new UIImageView (image);
            iv.Tag = MESSAGE_PART_TAG;
            view.AddSubview (iv);
        }

        string magic = @"
            var style = document.createElement(""style""); 
            document.head.appendChild(style); 
            style.innerHTML = ""html{-webkit-text-size-adjust: auto; word-wrap: break-word;}"";
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

            var wv = new UIWebView (new RectangleF (0, 0, View.Frame.Width, 1));
            wv.ScrollView.Bounces = false;
            wv.ScrollView.ScrollEnabled = true;
            wv.ScrollView.PagingEnabled = false;
            wv.ScrollView.MultipleTouchEnabled = false;
            wv.ContentMode = UIViewContentMode.ScaleAspectFit;
            wv.BackgroundColor = UIColor.White;
            wv.Tag = MESSAGE_PART_TAG;
            view.Add (wv);

            wv.LoadStarted += (object sender, EventArgs e) => {
                htmlBusy += 1;
            };

            wv.LoadFinished += (object sender, EventArgs e) => {
                htmlBusy -= 1;
                if (0 == htmlBusy) {
                    wv.EvaluateJavascript (magic);
                    var frame = wv.Frame;
                    frame.Width = View.Frame.Width;
                    frame.Height = (wv.ScrollView.ContentSize.Height > View.Bounds.Height) ? View.Bounds.Height : wv.ScrollView.ContentSize.Height;
                    wv.Frame = frame;
                    if (0 == DeferLayoutDecrement ()) {
                        LayoutView ();
                    }
                }
            };

            wv.LoadError += (object sender, UIWebErrorArgs e) => {
                htmlBusy -= 1;
                if (0 == DeferLayoutDecrement ()) {
                    LayoutView ();
                }
            };

            wv.ShouldStartLoad += delegate(UIWebView webView, NSUrlRequest request, UIWebViewNavigationType navigationType) {
                if (UIWebViewNavigationType.LinkClicked == navigationType) {
                    UIApplication.SharedApplication.OpenUrl (request.Url);
                    return false;
                }
                return true;
            };

            DeferLayoutIncrement ();
            wv.LoadHtmlString (html, null);
        }

        //        void RenderHtml (MimePart part)
        //        {
        //            var textPart = part as TextPart;
        //            var html = textPart.Text;
        //
        //            var nsError = new NSError ();
        //            var nsAttributes = new NSAttributedStringDocumentAttributes ();
        //            nsAttributes.DocumentType = NSDocumentType.HTML;
        //            var attributedString = new NSAttributedString (html, nsAttributes, ref nsError);
        //            var tv = new UITextView (new RectangleF (0, 0, View.Frame.Width, 1));
        //            tv.AttributedText = attributedString;
        //            tv.AutoresizingMask = UIViewAutoresizing.FlexibleBottomMargin;
        //            tv.UserInteractionEnabled = false;
        //            tv.SizeToFit ();
        //
        //            tv.Tag = MESSAGE_PART_TAG;
        //
        //            view.Add (tv);
        //        }

        /// Gets the decoded text content.
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
            dvc.View.Tag = MESSAGE_PART_TAG;
            view.AddSubview (dvc.View);
        }

        /// <summary>
        /// Map meeting uid to calendar record.
        /// </summary>
        void UpdateMeetingStatus (IEvent evt, NcAttendeeStatus status)
        {
            // TODO: Map meeting uid to calendar record; update status
        }


        protected void onAttachmentSelected (UITapGestureRecognizer obj)
        {
            var attachmentView = obj.View;
            var attachment = attachments [attachmentView.Tag];

            if (attachment.IsDownloaded) {
                PlatformHelpers.DisplayAttachment (this, attachment);
            } else {
                PlatformHelpers.DownloadAttachment (attachment);
            }
        }

        protected void onAttachmentIconSelected (UITapGestureRecognizer obj)
        {
            scrollView.ScrollRectToVisible (attachmentListView.Frame, true);
        }

        protected void DeferLayoutIncrement ()
        {
            lock (deferLayoutLock) {
                deferLayout += 1;
            }
        }

        protected int DeferLayoutDecrement ()
        {
            lock (deferLayoutLock) {
                deferLayout -= 1;
                return deferLayout;
            }
        }
    }
}
