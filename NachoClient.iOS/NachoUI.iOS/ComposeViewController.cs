// This file has been autogenerated from a class added in the UI designer.

using System;
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
    public partial class ComposeViewController : DialogViewController, INachoMessageController, INachoContactChooserDelegate
    {
        List<NcEmailAddress> AddressList = new List<NcEmailAddress> ();
        public static readonly NSString Reply = new NSString ("Reply");
        public static readonly NSString ReplyAll = new NSString ("ReplyAll");
        public static readonly NSString Forward = new NSString ("Forward");
        public string Action;
        public List<McEmailMessage> ActionThread;
        public INachoMessageControllerDelegate owner;

        public ComposeViewController (IntPtr handle) : base (handle)
        {
    
        }

        public void SetOwner (INachoMessageControllerDelegate o)
        {
            owner = o;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

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
        }

        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            NachoAssert.CaseError ();
        }

        Section toAddresses;
        Section ccAddresses;
        Section bccAddresses;
        SubjectEntryElement subjectEntryElement;
        MultilineEntryElement bodyElement;

        protected void ReloadRoot ()
        {
            var root = new RootElement ("New Message");

            ReloadAddresses ();

            root.Add (toAddresses);
            root.Add (ccAddresses);
            root.Add (bccAddresses);

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
           
            var body = new TextPart ("plain");
            var text = bodyElement.Summary ();
            if (null != text) {
                body.Text = text;
            }
            mimeMessage.Body = body;

            // TODO: Push account in UI
            // We only have one account, for now.
            var account = BackEnd.Instance.Db.Table<McAccount> ().First ();

            MimeHelpers.SendEmail (account.Id, mimeMessage);

            // SCORING - If not a reply, score each recipient. 
            // Should it be everyone in To, cc, bcc? Or just To?
            // Rignt now. Only To get score updated.
            if (null == Action) {
                UpdateScore (account.Id, "send to", +3);
            } else if (Action.Equals (Reply) || Action.Equals (ReplyAll)) {
                UpdateScore (account.Id, "reply", +2);
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

            var ActionMessage = ActionThread.First ();
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
            var ActionMessage = ActionThread.First ();

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
            var body = ActionMessage.GetBody ();
            if (null == body) {
                return;
            }
            if (Action.Equals (Forward)) {
                // TODO: Compose needs to be smart about MIME messages.
                bodyElement = new MultilineEntryElement ("Enter your message...", null, 120.0f, true);
                return;
            }
            string someText = MimeHelpers.FetchSomeText (body);
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
