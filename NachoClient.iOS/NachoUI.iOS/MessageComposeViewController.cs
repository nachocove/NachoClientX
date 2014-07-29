// This file has been autogenerated from a class added in the UI designer.

using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using MimeKit;

using NachoCore.Model;
using NachoCore.Utils;
using SWRevealViewControllerBinding;

namespace NachoClient.iOS
{
    public partial class MessageComposeViewController : UIViewController, IUcAddressBlockDelegate, IUcAttachmentBlockDelegate, INachoContactChooserDelegate, INachoFileChooserParent
    {

        public static readonly string Reply = "Reply";
        public static readonly string ReplyAll = "ReplyAll";
        public static readonly string Forward = "Forward";
        public string Action;
        public McEmailMessageThread ActionThread;
        public INachoMessageEditorParent owner;
        public bool showMenu;
        protected McAccount account;


        bool suppressLayout;
        float keyboardHeight;

        UcAddressBlock toView;
        UcAddressBlock ccView;
        UcAddressBlock bccView;
        UcAttachmentBlock attachmentView;

        UILabel subjectLabel;
        UITextField subjectField;
        UIButton priorityButton;

        UITextView bodyTextView;

        UIView toViewHR;
        UIView ccViewHR;
        UIView bccViewHR;
        UIView subjectLabelHR;
        UIView attachmentViewHR;

        UIToolbar keyboardToolbar;

        NcEmailAddress PresetToAddress;
        string PresetSubject;
        string EmailTemplate;

        protected float LINE_HEIGHT = 40;
        protected float LEFT_INDENT = 15;
        protected float RIGHT_INDENT = 15;

        public MessageComposeViewController (IntPtr handle) : base (handle)
        {
        }

        public void SetOwner (INachoMessageEditorParent o)
        {
            owner = o;
        }

        // Can be called by owner to set a pre-existing To: address, subject, and email template
        public void SetEmailAddressAndTemplate (NcEmailAddress toAddress, string subject = null, string emailTemplate = null)
        {
            PresetToAddress = toAddress;
            PresetSubject = subject;
            EmailTemplate = emailTemplate;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();

            if (showMenu) {
                // Navigation
                revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
                revealButton.Target = this.RevealViewController ();


                nachoButton.Clicked += (object sender, EventArgs e) => {
                    PerformSegue ("ComposeToNachoNow", this);
                };
                NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton, nachoButton };
            }

            NavigationItem.RightBarButtonItem = sendButton;

            attachButton.SetTitle (" Attach Files", UIControlState.Normal);
            attachButton.SizeToFit ();

            taskButton.SetTitle (" Set Reminder", UIControlState.Normal);
            taskButton.SizeToFit ();

            keyboardToolbar = new UIToolbar (NavigationController.Toolbar.Frame);
            var b1 = new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace);
            var b2 = new UIBarButtonItem (attachButton);
            var b3 = new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace);
            var b4 = new UIBarButtonItem (taskButton);
            var b5 = new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace);
            keyboardToolbar.SetItems (new UIBarButtonItem[] { b1, b2, b3, b4, b5 }, false);
            keyboardToolbar.BackgroundColor = A.Color_NachoBlack;
            keyboardToolbar.BarTintColor = A.Color_NachoBlack;

            attachButton.TouchUpInside += (object sender, EventArgs e) => {
                suppressLayout = true;
                View.EndEditing (true);
                suppressLayout = false;
                attachmentView.Hidden = false;
                attachmentViewHR.Hidden = false;
                attachmentView.SetCompact(false);
                attachmentView.ConfigureView ();
                attachmentView.PromptForAttachment("message");
            };

            sendButton.Clicked += (object sender, EventArgs e) => {
                SendMessage ();
            };

            CreateView ();

            if (null != ActionThread) {
                InitializeMessageForAction ();
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = false;
            }

            ConfigureFullView ();
        }

        public override UIView InputAccessoryView {
            get {
                return keyboardToolbar;
            }
        }

        public override bool CanBecomeFirstResponder {
            get {
                return true;
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillHideNotification);
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillShowNotification);
            }
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
        }

        public virtual bool HandlesKeyboardNotifications {
            get { return true; }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("ComposeToContactChooser")) {
                var dc = (INachoContactChooser)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var address = (NcEmailAddress)holder.value;
                dc.SetOwner (this, address, NachoContactType.EmailRequired);
                return;
            }
            if (segue.Identifier.Equals ("ComposeToAttachments")) {
                var dc = (INachoFileChooser)segue.DestinationViewController;
                dc.SetOwner (this);
                return;
            }
            if (segue.Identifier.Equals ("ComposeToFiles")) {
                var dc = (INachoFileChooser)segue.DestinationViewController;
                dc.SetOwner (this);
                return;
            }
            if (segue.Identifier.Equals ("ComposeToNachoNow")) {
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        /// IUcAttachmentBlock delegate
        public void PerformSegueForAttachmentBlock (string identifier, SegueHolder segueHolder)
        {
            PerformSegue (identifier, segueHolder);
        }

        /// IUcAttachmentBlock delegate
        public void DisplayAttachmentForAttachmentBlock (McAttachment attachment)
        {
            PlatformHelpers.DisplayAttachment (this, attachment);
        }

        /// IUcAttachmentBlock delegate
        public void PresentViewControllerForAttachmentBlock (UIViewController viewControllerToPresent, bool animated, NSAction completionHandler)
        {
            this.PresentViewController (viewControllerToPresent, animated, completionHandler);
        }

        protected void CreateView ()
        {
            scrollView.BackgroundColor = UIColor.White;
            contentView.BackgroundColor = UIColor.White;

            toView = new UcAddressBlock (this, "To:", View.Frame.Width);
            ccView = new UcAddressBlock (this, "Cc:", View.Frame.Width);
            bccView = new UcAddressBlock (this, "Bcc:", View.Frame.Width);

            toViewHR = new UIView (new RectangleF (0, 0, View.Frame.Width, 1));
            toViewHR.BackgroundColor = A.Color_NachoNowBackground;

            ccViewHR = new UIView (new RectangleF (0, 0, View.Frame.Width, 1));
            ccViewHR.BackgroundColor = A.Color_NachoNowBackground;

            bccViewHR = new UIView (new RectangleF (0, 0, View.Frame.Width, 1));
            bccViewHR.BackgroundColor = A.Color_NachoNowBackground;

            subjectLabelHR = new UIView (new RectangleF (0, 0, View.Frame.Width, 1));
            subjectLabelHR.BackgroundColor = A.Color_NachoNowBackground;

            attachmentViewHR = new UIView (new RectangleF (0, 0, View.Frame.Width, 1));
            attachmentViewHR.BackgroundColor = A.Color_NachoNowBackground;

            subjectLabel = new UILabel ();
            subjectLabel.Text = "Subject: ";
            subjectLabel.Font = A.Font_AvenirNextRegular14;
            subjectLabel.TextColor = A.Color_0B3239;
            subjectLabel.SizeToFit ();

            subjectField = new UITextField ();
            subjectField.Font = A.Font_AvenirNextRegular14;
            subjectField.TextColor = A.Color_808080;
            subjectField.Placeholder = "No subject";
            if (PresetSubject != null) {
                subjectField.Text += PresetSubject;
            }
            subjectField.SizeToFit ();

            priorityButton = UIButton.FromType (UIButtonType.ContactAdd);

            attachmentView = new UcAttachmentBlock (this, account.Id, View.Frame.Width);

            bodyTextView = new UITextView ();
            bodyTextView.Font = A.Font_AvenirNextRegular14;
            bodyTextView.TextColor = A.Color_808080;
            bodyTextView.ContentInset = new UIEdgeInsets (0, 15, 0, -15);
            if (EmailTemplate != null) {
                bodyTextView.InsertText (EmailTemplate);
            }
            bodyTextView.InsertText ("\n"+ "\n" +"This email sent by NachoMail");
            var beginningRange = new NSRange (0, 0);
            bodyTextView.SelectedRange = beginningRange;

            //Need to be able to inserthtml here, but for now will do simple text input
            //bodyTextView.InsertText ("<html><head></head><body>This message sent by <a href='http://www.nachocove.com'>NachoMail</a></body></html>");


            View.BackgroundColor = UIColor.White;

            contentView.AddSubviews (new UIView[] {
                toView,
                toViewHR,
                ccView,
                ccViewHR,
                bccView,
                bccViewHR,
                subjectLabel,
                subjectLabelHR,
                subjectField,
                priorityButton,
                attachmentView,
                attachmentViewHR,
                bodyTextView
            }); 

            subjectField.EditingDidBegin += (object sender, EventArgs e) => {
                ConfigureSubjectEditView ();
            };
                
            bodyTextView.Started += (object sender, EventArgs e) => {
                ConfigureBodyEditView ();
            };

            bodyTextView.Changed += (object sender, EventArgs e) => {
                SelectionChanged (bodyTextView);
            };

            if (PresetToAddress != null) {
                UpdateEmailAddress (PresetToAddress);
            }

//            attachmentView.BackgroundColor = UIColor.Yellow;
//            bodyTextView.BackgroundColor = UIColor.Gray;
//            contentView.BackgroundColor = UIColor.Green;
//            scrollView.BackgroundColor = UIColor.Red;
//            View.BackgroundColor = UIColor.Cyan;
        }

        protected void ConfigureFullView ()
        {
            toView.Hidden = false;
            ccView.Hidden = false;
            bccView.Hidden = false;
            attachmentView.Hidden = false;

            toView.SetCompact (false, -1);
            ccView.SetCompact (false, -1);
            bccView.SetCompact (false, -1);
            attachmentView.SetCompact (false);

            toViewHR.Hidden = false;
            ccViewHR.Hidden = false;
            bccViewHR.Hidden = false;
            attachmentViewHR.Hidden = false;

            suppressLayout = true;
            toView.ConfigureView ();
            ccView.ConfigureView ();
            bccView.ConfigureView ();
            attachmentView.ConfigureView ();
            suppressLayout = false;

            LayoutView ();
        }

        protected void ConfigureSubjectEditView ()
        {
            // If ccView & bccView are hidden,leave them that way.
            toView.Hidden = false;
            toViewHR.Hidden = false;
            // ccView.Hidden = false;
            // bccView.Hidden = false;

            toView.SetCompact (true, -1);
            ccView.SetCompact (true, -1);
            bccView.SetCompact (true, -1);
            attachmentView.SetCompact (true);

            suppressLayout = true;
            toView.ConfigureView ();
            ccView.ConfigureView ();
            bccView.ConfigureView ();
            attachmentView.ConfigureView ();
            suppressLayout = false;

            LayoutView ();
        }

        protected void ConfigureBodyEditView ()
        {
            // this might be the place that we set up our initializaiton text
            toView.SetCompact (true, -1);

            toView.Hidden = false;
            ccView.Hidden = true;
            bccView.Hidden = true;
            attachmentView.Hidden = true;

            toViewHR.Hidden = false;
            ccViewHR.Hidden = true;
            bccViewHR.Hidden = true;
            attachmentViewHR.Hidden = true;



            suppressLayout = true;
            toView.ConfigureView ();
            ccView.ConfigureView ();
            bccView.ConfigureView ();
            attachmentView.ConfigureView ();
            suppressLayout = false;

            LayoutView ();
        }

        /// IUcAttachmentBlock delegate
        public void AttachmentBlockNeedsLayout (UcAttachmentBlock view)
        {
            if (suppressLayout) {
                return;
            }
            LayoutView ();
        }

        public void AddressBlockNeedsLayout (UcAddressBlock view)
        {
            if (suppressLayout) {
                return;
            }
            LayoutView ();
        }

        public void AddressBlockWillBecomeActive (UcAddressBlock view)
        {
            ConfigureFullView ();
        }

        public void AddressBlockWillBecomeInactive (UcAddressBlock view)
        {
        }

        protected void LayoutView ()
        {
            if (suppressLayout) {
                return;
            }

            UIView.Animate (0.2, () => {
                toView.Layout ();
                ccView.Layout ();
                bccView.Layout ();
                attachmentView.Layout ();

                float yOffset = 0;

                if (!toView.Hidden) {
                    AdjustY (toView, yOffset);
                    yOffset += toView.Frame.Height;
                    AdjustY (toViewHR, yOffset);
                    yOffset += toViewHR.Frame.Height;
                }

                if (!ccView.Hidden) {
                    AdjustY (ccView, yOffset);
                    yOffset += ccView.Frame.Height;
                    AdjustY (ccViewHR, yOffset);
                    yOffset += ccViewHR.Frame.Height;
                }

                if (!bccView.Hidden) {
                    AdjustY (bccView, yOffset);
                    yOffset += bccView.Frame.Height;
                    AdjustY (bccViewHR, yOffset);
                    yOffset += bccViewHR.Frame.Height;
                }

                CenterY (subjectLabel, LEFT_INDENT, yOffset, subjectLabel.Frame.Width, LINE_HEIGHT);
                CenterY (priorityButton, View.Frame.Width - priorityButton.Frame.Width - RIGHT_INDENT, yOffset, priorityButton.Frame.Width, LINE_HEIGHT);

                var subjectFieldStart = subjectLabel.Frame.X + subjectLabel.Frame.Width;
                var subjectFieldWidth = priorityButton.Frame.X - subjectFieldStart;
                CenterY (subjectField, subjectFieldStart, yOffset, subjectFieldWidth, LINE_HEIGHT);
                yOffset += LINE_HEIGHT;

                AdjustY (subjectLabelHR, yOffset);
                yOffset += subjectLabelHR.Frame.Height;

                if (!attachmentView.Hidden) {
                    AdjustY (attachmentView, yOffset);
                    yOffset += attachmentView.Frame.Height;
                    AdjustY (attachmentViewHR, yOffset);
                    yOffset += attachmentViewHR.Frame.Height;
                }

                var bodyTextViewHeight = View.Frame.Height - keyboardHeight;
                bodyTextView.Frame = new RectangleF (0, yOffset, View.Frame.Width, bodyTextViewHeight);
                yOffset += bodyTextViewHeight;
                bodyTextView.ScrollEnabled = true;

                scrollView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);

                var contentFrame = new RectangleF (0, 0, View.Frame.Width, yOffset);
                contentView.Frame = contentFrame;
                scrollView.ContentSize = contentFrame.Size;
            });
        }

        protected void AdjustY (UIView view, float yOffset)
        {
            var frame = view.Frame;
            frame.Y = yOffset;
            view.Frame = frame;
        }

        protected void CenterY (UIView view, float x, float y, float width, float section_height)
        {
            var centeredY = y + (section_height / 2) - (view.Frame.Height / 2);
            view.Frame = new RectangleF (x, centeredY, width, view.Frame.Height);
        }

        private void OnKeyboardNotification (NSNotification notification)
        {
            if (IsViewLoaded) {
                //Check if the keyboard is becoming visible
                bool visible = notification.Name == UIKeyboard.WillShowNotification;
                //Start an animation, using values from the keyboard
                UIView.BeginAnimations ("AnimateForKeyboard");
                UIView.SetAnimationBeginsFromCurrentState (true);
                UIView.SetAnimationDuration (UIKeyboard.AnimationDurationFromNotification (notification));
                UIView.SetAnimationCurve ((UIViewAnimationCurve)UIKeyboard.AnimationCurveFromNotification (notification));
                //Pass the notification, calculating keyboard height, etc.
                bool landscape = InterfaceOrientation == UIInterfaceOrientation.LandscapeLeft || InterfaceOrientation == UIInterfaceOrientation.LandscapeRight;
                if (visible) {
                    var keyboardFrame = UIKeyboard.FrameEndFromNotification (notification);
                    OnKeyboardChanged (visible, landscape ? keyboardFrame.Width : keyboardFrame.Height);
                } else {
                    var keyboardFrame = UIKeyboard.FrameBeginFromNotification (notification);
                    OnKeyboardChanged (visible, landscape ? keyboardFrame.Width : keyboardFrame.Height);
                }
                //Commit the animation
                UIView.CommitAnimations (); 
            }
        }

        /// <summary>
        /// Override this method to apply custom logic when the keyboard is shown/hidden
        /// </summary>
        /// <param name='visible'>
        /// If the keyboard is visible
        /// </param>
        /// <param name='height'>
        /// Calculated height of the keyboard (width not generally needed here)
        /// </param>
        protected virtual void OnKeyboardChanged (bool visible, float height)
        {
            var newHeight = (visible ? height : 0);

            if (newHeight == keyboardHeight) {
                return;
            }
            keyboardHeight = newHeight;

            LayoutView ();
        }

        /// <summary>
        ///  Called when a key is pressed (or other changes) in body text view
        /// </summary>
        protected void SelectionChanged (UITextView textView)
        {
            // We want to scroll the caret rect into view
            var caretRect = textView.GetCaretRectForPosition (textView.SelectedTextRange.end);
            caretRect.Size = new SizeF (caretRect.Size.Width, caretRect.Size.Height + textView.TextContainerInset.Bottom);
            // Make sure our textview is big enough to hold the text
            var frame = textView.Frame;
            frame.Size = new SizeF (textView.ContentSize.Width, textView.ContentSize.Height + 40);
            textView.Frame = frame;
            // And update our enclosing scrollview for the new content size
            scrollView.ContentSize = new SizeF (scrollView.ContentSize.Width, textView.Frame.Y + textView.Frame.Height);
            // Adjust the caretRect to be in our enclosing scrollview, and then scroll it
            caretRect.Y += textView.Frame.Y;
            scrollView.ScrollRectToVisible (caretRect, true);
        }

        /// IUcAddressBlock delegate
        public void AddressBlockAddContactClicked (UcAddressBlock view, string prefix)
        {
            NcEmailAddress.Kind kind = NcEmailAddress.Kind.Unknown;

            if (view == toView) {
                kind = NcEmailAddress.Kind.To;
            } else if (view == ccView) {
                kind = NcEmailAddress.Kind.Cc;
            } else if (view == bccView) {
                kind = NcEmailAddress.Kind.Bcc;
            } else {
                NcAssert.CaseError ();
            }
            var e = new NcEmailAddress (kind);
            e.action = NcEmailAddress.Action.create;
            e.address = prefix;
            PerformSegue ("ComposeToContactChooser", new SegueHolder (e));
        }

        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            NcAssert.CaseError ();
        }

        /// <summary>
        /// INachoContactChooser callback
        /// </summary>
        public void UpdateEmailAddress (NcEmailAddress address)
        {
            NcAssert.True (null != address);

            switch (address.kind) {
            case NcEmailAddress.Kind.To:
                toView.Append (address);
                break;
            case NcEmailAddress.Kind.Cc:
                ccView.Append (address);
                break;
            case NcEmailAddress.Kind.Bcc:
                bccView.Append (address);
                break;
            default:
                NcAssert.CaseError ();
                break;
            }
        }

        /// <summary>
        /// Callback
        /// </summary>
        public void DeleteEmailAddress (NcEmailAddress address)
        {
            // Chooser returned an empty stirng; ignore it.
        }

        /// <summary>
        /// Backend is converting to mime.
        /// TODO: SendMessage should encode as mime or not.
        /// </summary>
        public void SendMessage ()
        {
            var mimeMessage = new MimeMessage ();

            /* var sentfrom = new TextPart ("html", "<html><head></head><body>This message sent by <a href='http://www.nachocove.com'>NachoMail</a></body></html>");
           
            var multipart = new Multipart ();
            */

            /* below lines break web-async. Need to see why
             if (sentfrom.ContentDisposition == null) {
                sentfrom.ContentDisposition = new ContentDisposition (ContentDisposition.Inline);
            }
            */

            foreach (var view in new UcAddressBlock[] { toView, ccView, bccView }) {
                foreach (var a in view.AddressList) {
                    var mailbox = a.ToMailboxAddress ();
                    if (null == mailbox) {
                        continue;
                    }
                    switch (a.kind) {
                    case NcEmailAddress.Kind.To:
                        mimeMessage.To.Add (mailbox);
                        break;
                    case NcEmailAddress.Kind.Cc:
                        mimeMessage.Cc.Add (mailbox);
                        break;
                    case NcEmailAddress.Kind.Bcc:
                        mimeMessage.Bcc.Add (mailbox);
                        break;
                    default:
                        NcAssert.CaseError ();
                        break;
                    }
                }
            }
            mimeMessage.Subject = Pretty.SubjectString (subjectField.Text);
            mimeMessage.Date = System.DateTime.UtcNow;

            var body = new BodyBuilder ();

            body.TextBody = bodyTextView.Text;

            foreach (var attachment in attachmentView.AttachmentList) {
                body.Attachments.Add (attachment.FilePath ());
            }

            //multipart.Add (body.ToMessageBody());

            //multipart.Add (sentfrom);

            //mimeMessage.Body = multipart;
            mimeMessage.Body = body.ToMessageBody ();

            MimeHelpers.SendEmail (account.Id, mimeMessage);

            // Might want to defer until BE says message is queued.
            owner = null;
            NavigationController.PopViewControllerAnimated (true);
        }

        // TODO: Put in pretty
        protected string CreateInitialSubjectLine ()
        {
            if (null == ActionThread) {
                return ""; // Creating a message
            }

            var ActionMessage = ActionThread.SingleMessageSpecialCase ();
            NcAssert.True (null != ActionMessage);

            var Subject = "";
            if (null != ActionMessage.Subject) {
                Subject = ActionMessage.Subject;
            }

            if (Action.Equals (Reply) || Action.Equals (ReplyAll)) {
                if (Subject.StartsWith ("Re:")) {
                    return Subject;
                }
                return "Re: " + Subject;
            }
            if (Action.Equals (Forward)) {
                return "Fwd: " + Subject;
            }
            return "";
        }

        /// <summary>
        /// Reply, ReplyAll, Forward
        /// FIXME:  Wait for full text to arrive!
        /// </summary>
        void InitializeMessageForAction ()
        {
            var ActionMessage = ActionThread.SingleMessageSpecialCase ();

            if (Action.Equals (Reply) || Action.Equals (ReplyAll)) {
                toView.Append (new NcEmailAddress (NcEmailAddress.Kind.To, ActionMessage.From));
            }
            if (Action.Equals (ReplyAll)) {
                // Add the To list to the CC list
                if (null != ActionMessage.To) {
                    string[] ToList = ActionMessage.To.Split (new Char [] { ',' });
                    if (null != ToList) {
                        foreach (var a in ToList) {
                            ccView.Append (new NcEmailAddress (NcEmailAddress.Kind.Cc, a));
                        }
                    }
                }
                // And keep the existing CC list
                if (null != ActionMessage.Cc) {
                    string[] ccList = ActionMessage.Cc.Split (new Char [] { ',' });
                    if (null != ccList) {
                        foreach (var a in ccList) {
                            ccView.Append (new NcEmailAddress (NcEmailAddress.Kind.Cc, a));
                        }
                    }
                }
            }
            // TODO: Setup message id, etc etc.
            // Handle body
            if (Action.Equals (Forward)) {
                // TODO: Compose needs to be smart about MIME messages.
                string fwdText = MimeHelpers.ExtractTextPart (ActionMessage);
                string fwdquotedText = QuoteForReply (fwdText);
                bodyTextView.Text = fwdquotedText;
                return;
            }

            string someText = MimeHelpers.ExtractTextPart (ActionMessage);
            string quotedText = QuoteForReply (someText);
            bodyTextView.Text = quotedText;
        }

        string QuoteForReply (string s)
        {
            if (null == s) {
                return s;
            }
            string[] lines = s.Split (new Char[] { '\n' });
            string quotes = "\n> " + String.Join ("\n> ", lines);
            return quotes;
        }

        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void SelectFile (INachoFileChooser vc, McAbstrObject obj)
        {
            // Attachment
            var a = obj as McAttachment;
            if (null != a) {
                attachmentView.Append (a);
                vc.DismissFileChooser (true, null);
                return;
            }

            // File
            var file = obj as McFile;
            if (null != file) {
                var attachment = new McAttachment ();
                attachment.DisplayName = file.DisplayName;
                attachment.AccountId = account.Id;
                attachment.Insert ();
                var guidString = Guid.NewGuid ().ToString ("N");
                // TODO: Decide on copy, move, delete, etc
                File.Copy (file.FilePath (), McAttachment.TempPath (guidString));
                //                File.Move (file.FilePath (), McAttachment.TempPath (guidString));
                //                file.Delete ();
                attachment.SaveFromTemp (guidString);
                attachment.Update ();
                attachmentView.Append (attachment);
                vc.DismissFileChooser (true, null);
                return;
            }
            NcAssert.CaseError ();
        }

        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void DismissChildFileChooser (INachoFileChooser vc)
        {
            vc.DismissFileChooser (true, null);
        }

    }
}
