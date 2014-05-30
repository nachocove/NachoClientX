// This file has been autogenerated from a class added in the UI designer.

using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using MimeKit;

namespace NachoClient.iOS
{
    public partial class ComposeViewController : NcDialogViewController, INachoContactChooserDelegate, INachoFileChooserParent
    {
        List<NcEmailAddress> AddressList = new List<NcEmailAddress> ();
        List<int> attachmentList = new List<int> ();
        public static readonly NSString Reply = new NSString ("Reply");
        public static readonly NSString ReplyAll = new NSString ("ReplyAll");
        public static readonly NSString Forward = new NSString ("Forward");
        public string Action;
        public McEmailMessageThread ActionThread;
        public INachoMessageEditorParent owner;
        protected McAccount account;

        public ComposeViewController (IntPtr handle) : base (handle)
        {
    
        }

        public void SetOwner (INachoMessageEditorParent o)
        {
            owner = o;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            account = NcModel.Instance.Db.Table<McAccount> ().First ();

            Style = UITableViewStyle.Plain;
            TableView.SeparatorInset = new UIEdgeInsets (0, 0, 0, 0);

            // Closes the multi-line edit view!
            var tap = new UITapGestureRecognizer ();
            tap.AddTarget (() => {
                this.View.EndEditing (true);
            });
            tap.CancelsTouchesInView = false;
            this.View.AddGestureRecognizer (tap);

            SendButton.Clicked += (object sender, EventArgs e) => {
                SendMessage ();
            };

            if (null != ActionThread) {
                InitializeMessageForAction ();
            }

            Pushing = true;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            ReloadRoot ();
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("ComposeToContactChooser")) {
                var dc = (INachoContactChooser)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var address = (NcEmailAddress)holder.value;
                dc.SetOwner (this, address, NachoContactType.EmailRequired);
            }
            if (segue.Identifier.Equals ("ComposeToAttachments")) {
                var dc = (INachoFileChooser)segue.DestinationViewController;
                dc.SetOwner (this);
            }
            if (segue.Identifier.Equals ("ComposeToFiles")) {
                var dc = (INachoFileChooser)segue.DestinationViewController;
                dc.SetOwner (this);
            }
        }

        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            NachoAssert.CaseError ();
        }

        Section toAddresses;
        Section ccAddresses;
        Section bccAddresses;
        Section attachments;
        SubjectEntryElement subjectEntryElement;
        MultilineEntryElement bodyElement;

        protected void ReloadRoot ()
        {
            var root = new RootElement ("New Message");

            ReloadAddresses ();

            root.Add (toAddresses);
            root.Add (ccAddresses);
            root.Add (bccAddresses);

            attachments = new SectionWithLineSeparator ();
            root.Add (attachments);
            ReloadAttachments ();

            var section = new SectionWithLineSeparator ();
            root.Add (section);

            if (null == subjectEntryElement) {
                var initialSubject = CreateInitialSubjectLine ();
                subjectEntryElement = new SubjectEntryElement (initialSubject);
            } else {
                subjectEntryElement = new SubjectEntryElement (subjectEntryElement.Value);
            }
            section.Add (subjectEntryElement);

            if (null == bodyElement) {
                bodyElement = new MultilineEntryElement ("Enter your message...", "", 120.0f, true);
            } else {
                bodyElement = new MultilineEntryElement ("Enter your message...", bodyElement.Summary (), 120.0f, true);
            }
            section.Add (bodyElement);

            root.UnevenRows = true;
            Root = root;
        }

        protected void ReloadAddresses ()
        {
            toAddresses = AddressSection (NcEmailAddress.Kind.To);
            ccAddresses = AddressSection (NcEmailAddress.Kind.Cc);
            bccAddresses = AddressSection (NcEmailAddress.Kind.Bcc);

            for (int i = 0; i < AddressList.Count; i++) {
                var a = AddressList [i];
                a.index = i;
                a.action = NcEmailAddress.Action.edit;
                var e = new StringElement (a.address);
                e.Tapped += () => {
                    PerformSegue ("ComposeToContactChooser", new SegueHolder (a));
                };
                switch (a.kind) {
                case NcEmailAddress.Kind.To:
                    toAddresses.Add (e);
                    break;
                case NcEmailAddress.Kind.Cc:
                    ccAddresses.Add (e);
                    break;
                case NcEmailAddress.Kind.Bcc:
                    bccAddresses.Add (e);
                    break;
                default:
                    NachoAssert.CaseError ();
                    break;
                }
            }
        }

        /// <summary>
        /// Create a section for To, CC, and Bcc
        /// with special cell to trigger a new address.
        /// </summary>
        public Section AddressSection (NcEmailAddress.Kind kind)
        {
            var s = new SectionWithLineSeparator ();
            using (var image = UIImage.FromBundle ("ic_action_add_person")) {
                var scaledImage = image.Scale (new SizeF (22.0f, 22.0f));
                var e = new StyledStringElementWithIcon (NcEmailAddress.ToPrefix (kind), scaledImage);
                e.BackgroundColor = UIColor.LightTextColor;
                e.Tapped += () => {
                    var address = new NcEmailAddress (kind);
                    address.action = NcEmailAddress.Action.create;
                    PerformSegue ("ComposeToContactChooser", new SegueHolder (address));
                };
                s.Add (e);
            }
            return s;
        }

        protected void ReloadAttachments ()
        {
            attachments.Clear ();
            using (var image = UIImage.FromBundle ("ic_action_attachment")) {
                var scaledImage = image.Scale (new SizeF (22.0f, 22.0f));
                var e = new StyledStringElementWithIcon ("Attachments", scaledImage);
                e.BackgroundColor = UIColor.LightTextColor;
                e.Tapped += () => {
                    AttachFileActionSheet ();
                };
                attachments.Add (e);
            }
         
            // List of Attachments, index 1..N
            for (int i = 0; i < attachmentList.Count; i++) {
                var attachmentIndex = attachmentList [i];
                var attachment = McAttachment.QueryById<McAttachment> (attachmentIndex);
                var e = new StringElement (attachment.DisplayName);
                var index = i;
                e.Tapped += () => {
                    AttachmentActionSheet (index);
                };
                attachments.Add (e);
            }
        }

        protected void AttachmentActionSheet (int i)
        {
            var actionSheet = new UIActionSheet ("Attachment Manager");
            actionSheet.Add ("Cancel");
            actionSheet.Add ("Remove Attachment");
            actionSheet.Add ("Preview Attachment");

            actionSheet.Clicked += delegate(object a, UIButtonEventArgs b) {
                switch (b.ButtonIndex) {
                case 0:
                    break; // Cancel
                case 1:
                    attachmentList.RemoveAt (i);
                    ReloadAttachments ();
                    Root.Reload (attachments, UITableViewRowAnimation.Automatic);
                    break;
                case 2:
                    var attachmentIndex = attachmentList [i];
                    var attachment = McAttachment.QueryById<McAttachment> (attachmentIndex);
                    PlatformHelpers.DisplayAttachment (this, attachment);
                    break;

                }
            };

            actionSheet.ShowInView (this.View);
        }

        protected void AttachFileActionSheet ()
        {
            var actionSheet = new UIActionSheet ("Attachment Manager");
            actionSheet.Add ("Cancel");
            actionSheet.Add ("Add Photo");
            actionSheet.Add ("Add Shared File");
            actionSheet.Add ("Add Existing Attachment");
            actionSheet.CancelButtonIndex = 0;

            actionSheet.Clicked += delegate(object sender, UIButtonEventArgs b) {
                switch (b.ButtonIndex) {
                case 0:
                    break; // Cancel
                case 1:
                    SetupPhotoPicker ();
                    break;
                case 2:
                    PerformSegue ("ComposeToFiles", new SegueHolder (null));
                    break;
                case 3:
                    PerformSegue ("ComposeToAttachments", new SegueHolder (null));
                    break;
                default:
                    NachoAssert.CaseError ();
                    break;
                }
            };

            actionSheet.ShowInView (this.View);
        }

        void SetupPhotoPicker ()
        {
            var imagePicker = new UIImagePickerController ();

            imagePicker.SourceType = UIImagePickerControllerSourceType.PhotoLibrary;

            imagePicker.FinishedPickingMedia += Handle_FinishedPickingMedia;
            imagePicker.Canceled += Handle_Canceled;

            imagePicker.ModalPresentationStyle = UIModalPresentationStyle.CurrentContext;
            NavigationController.PresentViewController (imagePicker, true, null);
        }

        void Handle_Canceled (object sender, EventArgs e)
        {
            var imagePicker = sender as UIImagePickerController;
            imagePicker.DismissViewController (true, null);
        }

        protected void Handle_FinishedPickingMedia (object sender, UIImagePickerMediaPickedEventArgs e)
        {
            var imagePicker = sender as UIImagePickerController;

            bool isImage = false;
            switch (e.Info [UIImagePickerController.MediaType].ToString ()) {
            case "public.image":
                isImage = true;
                break;
            case "public.video":
                // TODO: Implement videos
                Log.Info (Log.LOG_UI, "video ignored");
                break;
            default:
                // TODO: Implement videos
                Log.Error (Log.LOG_UI, "unknown media type selected");
                break;
            }

            if (isImage) {
                var image = e.Info [UIImagePickerController.EditedImage] as UIImage;
                if (null == image) {
                    image = e.Info [UIImagePickerController.OriginalImage] as UIImage;
                }
                NachoAssert.True (null != image);
                var attachment = new McAttachment ();
                attachment.AccountId = account.Id;
                attachment.Insert ();
                attachment.DisplayName = attachment.Id.ToString () + ".jpg";
                var guidString = Guid.NewGuid ().ToString ("N");
                using (var stream = McAttachment.TempFileStream (guidString)) {
                    using (var jpg = image.AsJPEG ().AsStream ()) {
                        jpg.CopyTo (stream);
                        jpg.Close ();
                    }
                }
                attachment.SaveFromTemp (guidString);
                attachment.Update ();
                attachmentList.Add (attachment.Id);
            }

            e.Info.Dispose ();
            imagePicker.DismissViewController (true, null);
        }

        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void SelectFile (INachoFileChooser vc, McObject obj)
        {
            var a = obj as McAttachment;
            if (null != a) {
                attachmentList.Add (a.Id);
            }
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
                attachmentList.Add (attachment.Id);
            }

            vc.DismissFileChooser (true, null);
        }

        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void DismissChildFileChooser (INachoFileChooser vc)
        {
            vc.DismissFileChooser (true, null);
        }

        /// <summary>
        /// Callback
        /// </summary>
        public void UpdateEmailAddress (NcEmailAddress address)
        {
            NachoAssert.True (null != address);

            switch (address.action) {
            case NcEmailAddress.Action.edit:
                AddressList [address.index] = address;
                break;
            case NcEmailAddress.Action.create:
                AddressList.Add (address);
                break;
            default:
                NachoAssert.CaseError ();
                break;
            }
        }

        /// <summary>
        /// Callback
        /// </summary>
        public void DeleteEmailAddress (NcEmailAddress address)
        {
            NachoAssert.True (null != address);

            if (NcEmailAddress.Action.edit == address.action) {
                AddressList.RemoveAt (address.index);
            }
        }

        /// <summary>
        /// Backend is converting to mime.
        /// TODO: SendMessage should encode as mime or not.
        /// </summary>
        public void SendMessage ()
        {
            var mimeMessage = new MimeMessage ();

            foreach (var a in AddressList) {
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
                    NachoAssert.CaseError ();
                    break;
                }
            }
            mimeMessage.Subject = subjectEntryElement.Value;
            mimeMessage.Date = System.DateTime.UtcNow;

            var body = new BodyBuilder ();

            body.TextBody = bodyElement.Summary ();
            foreach (var attachmentIndex in attachmentList) {
                var attachment = McAttachment.QueryById<McAttachment> (attachmentIndex);
                body.Attachments.Add (attachment.FilePath ());
            }

            mimeMessage.Body = body.ToMessageBody ();

            MimeHelpers.SendEmail (account.Id, mimeMessage);

            // SCORING - If not a reply, score each recipient. 
            // Should it be everyone in To, cc, bcc? Or just To?
            // Rignt now. Only To get score updated.
            if (null == Action) {
                UpdateScore (account.Id, "send to", +3);
            } else if (Action.Equals (Reply) || Action.Equals (ReplyAll)) {
                UpdateScore (account.Id, "reply", +2);
            } else if (Action.Equals (Forward)) {
                UpdateScore (account.Id, "forward", +1);
            }

            // Might want to defer until BE says message is queued.
            owner = null;
            NavigationController.PopViewControllerAnimated (true);
        }

        private void UpdateScore (int id, string reason, int score)
        {
            foreach (var a in AddressList) {
                var mailbox = a.ToMailboxAddress ();
                if (null == mailbox) {
                    continue;
                }
                if (NcEmailAddress.Kind.To != a.kind) {
                    continue;
                }
                List<McContact> contactList = McContact.QueryByEmailAddress (id, mailbox.Address);
                foreach (McContact contact in contactList) {
                    contact.UpdateScore (reason, score);
                }
            }
        }

        protected string CreateInitialSubjectLine ()
        {
            if (null == ActionThread) {
                return ""; // Creating a message
            }

            var ActionMessage = ActionThread.SingleMessageSpecialCase ();
            NachoAssert.True (null != ActionMessage);

            if (Action.Equals (Reply) || Action.Equals (ReplyAll)) {
                if (ActionMessage.Subject.StartsWith ("Re:")) {
                    return ActionMessage.Subject;
                }
                return "Re: " + ActionMessage.Subject;
            }
            if (Action.Equals (Forward)) {
                return "Fwd: " + ActionMessage.Subject;
            }
            return "";
        }

        /// <summary>
        /// Reply, ReplyAll, Forward
        /// </summary>
        void InitializeMessageForAction ()
        {
            var ActionMessage = ActionThread.SingleMessageSpecialCase ();

            if (Action.Equals (Reply) || Action.Equals (ReplyAll)) {
                AddressList.Add (new NcEmailAddress (NcEmailAddress.Kind.To, ActionMessage.From));
            }
            if (Action.Equals (ReplyAll)) {
                // Add the To list to the CC list
                if (null != ActionMessage.To) {
                    string[] ToList = ActionMessage.To.Split (new Char [] { ',' });
                    if (null != ToList) {
                        foreach (var a in ToList) {
                            AddressList.Add (new NcEmailAddress (NcEmailAddress.Kind.Cc, a));
                        }
                    }
                }
                // And keep the existing CC list
                if (null != ActionMessage.Cc) {
                    string[] ccList = ActionMessage.Cc.Split (new Char [] { ',' });
                    if (null != ccList) {
                        foreach (var a in ccList) {
                            AddressList.Add (new NcEmailAddress (NcEmailAddress.Kind.Cc, a));
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
                bodyElement = new MultilineEntryElement ("Enter your message...", fwdquotedText, 120.0f, true);
                return;
            }
            string someText = MimeHelpers.ExtractTextPart (ActionMessage);
            string quotedText = QuoteForReply (someText);
            bodyElement = new MultilineEntryElement ("Enter your message...", quotedText, 120.0f, true);
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
    }
}
