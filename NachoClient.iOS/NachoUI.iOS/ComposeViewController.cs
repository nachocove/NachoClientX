// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using NachoCore.Model;
using NachoCore;
using MimeKit;

namespace NachoClient.iOS
{
    public partial class ComposeViewController : DialogViewController
    {
        string Subject;
        MultilineEntryElement Message;
        List<NcEmailAddress> AddressList = new List<NcEmailAddress> ();

        public ComposeViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            Style = UITableViewStyle.Plain;

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
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            ReloadRoot ();
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("ComposeToContactChooser")) {
                var index = ((NSNumber)sender).Int32Value;
                ContactChooserViewController destinationController = (ContactChooserViewController)segue.DestinationViewController;
                destinationController.owner = this;
                destinationController.ownerIndex = index;
            }
        }

        protected void ReloadRoot ()
        {
            var root = new RootElement ("New Message");

            var section = new Section ();
            root.Add (section);

            foreach (var c in AddressList) {
                if (c.kind.Equals (NcEmailAddress.Kind.To)) {
                    var e = CreateContactElement ("To: ", c, AddressList.IndexOf (c));
                    section.Add (e);
                }
            }
            section.Add (CreateContactElement ("To: ", null, AddressList.Count + 1));

            foreach (var c in AddressList) {
                if (c.kind.Equals (NcEmailAddress.Kind.Cc)) {
                    var e = CreateContactElement ("Cc: ", c, AddressList.IndexOf (c));
                    section.Add (e);
                }
            }
            section.Add (CreateContactElement ("Cc: ", null, AddressList.Count + 2));

            foreach (var c in AddressList) {
                if (c.kind.Equals (NcEmailAddress.Kind.Bcc)) {
                    var e = CreateContactElement ("Bcc: ", c, AddressList.IndexOf (c));
                    section.Add (e);
                }
            }
            section.Add (CreateContactElement ("Bcc: ", null, AddressList.Count + 3));

            var subjectEntry = new EntryElement ("Subject: ", "", Subject);
            subjectEntry.Changed += delegate(object sender, EventArgs e) {
                EntryElement o = (EntryElement)sender;
                Subject = o.Value;
            };
            section.Add (subjectEntry);

            var s = (null == Message) ? null : Message.Summary ();
            Message = new MultilineEntryElement ("Enter your message....", s, 120.0f, true);
            section.Add (Message);

            root.UnevenRows = true;
            Root = root;
        }

        protected StringElement CreateContactElement (string prefix, NcEmailAddress address, int index)
        {
            var s = prefix + (null == address ? "" : address.address);
            var e = new StringElement (s);
            e.Tapped += delegate {
                AddressTapped (index);
            };
            return e;
        }

        protected McContact TextContact (string txt)
        {
            var mc = new McContact ();
            mc.AddEmailAddressAttribute ("", "", txt);
            return mc;
        }

        protected void AddressTapped (int index)
        {
            PerformSegue ("ComposeToContactChooser", new NSNumber (index));
        }

        public NcEmailAddress GetEmailAddress (int index)
        {
            if (AddressList.Count > index) {
                return AddressList [index];
            }
            if ((AddressList.Count + 1) == index) {
                return new NcEmailAddress (NcEmailAddress.Kind.To);
            }
            if ((AddressList.Count + 2) == index) {
                return new NcEmailAddress (NcEmailAddress.Kind.Cc);
            }
            if ((AddressList.Count + 3) == index) {
                return new NcEmailAddress (NcEmailAddress.Kind.Bcc);
            }
            NachoAssert.CaseError ();
            return null;
        }

        public void ReplaceEmailAddress (int index, NcEmailAddress address)
        {
            if (AddressList.Count < index) {
                AddressList.Add (address);
            } else {
                AddressList.RemoveAt (index);
                AddressList.Insert (index, address);
            }
        }

        public void DeleteEmailAddress (int index)
        {
            if (AddressList.Count > index) {
                AddressList.RemoveAt (index);
            }
        }

        public MailboxAddress GetMailboxAddress (NcEmailAddress address)
        {
            NachoAssert.True ((null != address.contact) || (null != address.address));

            string name;
            string email;

            if (null == address.contact) {
                name = address.address;
                email = address.address;
            } else {
                name = address.contact.DisplayName;
                if (null == address.address) {
                    email = address.contact.DisplayEmailAddress;
                } else {
                    email = address.address;
                }
            }
            return new MailboxAddress (name, email);
        }

        public void SendMessage ()
        {
            var message = new MimeMessage ();

            foreach (var a in AddressList) {
                switch (a.kind) {
                case NcEmailAddress.Kind.To:
                    message.To.Add (GetMailboxAddress (a));
                    break;
                case NcEmailAddress.Kind.Cc:
                    message.Cc.Add (GetMailboxAddress (a));
                    break;
                case NcEmailAddress.Kind.Bcc:
                    message.Bcc.Add (GetMailboxAddress (a));
                    break;
                default:
                    NachoAssert.CaseError ();
                    break;
                }
            }
            if (null != Subject) {
                message.Subject = Subject;
            }
            message.Date = System.DateTime.UtcNow;

            // TODO: Send the message

            // Probably want to defer until BE says message is queued.
            NavigationController.PopViewControllerAnimated (true);
        }
    }
}
