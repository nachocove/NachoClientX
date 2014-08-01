// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class AttendeeViewController : NcDialogViewController, INachoContactChooserDelegate
    {
        List<McAttendee> AttendeeList = new List<McAttendee> ();

        public void SetAttendeeList (List<McAttendee> attendees)
        {
            this.AttendeeList = new List<McAttendee> ();
            foreach (var attendee in attendees) {
                this.AttendeeList.Add (attendee);
            }
        }

        public List<McAttendee> GetAttendeeList ()
        {
            return this.AttendeeList;
        }

        public AttendeeViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            this.Pushing = true;
            this.Style = UITableViewStyle.Plain;
            TableView.SeparatorInset = new UIEdgeInsets (0, 0, 0, 0);
            TableView.SeparatorColor = A.Color_NachoSeparator;
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
            if (segue.Identifier.Equals ("AttendeeViewToContactChooser")) {
                var dc = (INachoContactChooser)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var address = (NcEmailAddress)holder.value;
                dc.SetOwner (this, address, NachoContactType.EmailRequired);
            }
        }

        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            NcAssert.CaseError ();
        }

        Section requiredAddresses;
        Section optionalAddresses;

        protected void ReloadRoot ()
        {
            var root = new RootElement ("");

            ReloadAddresses ();

            root.Add (requiredAddresses);
            root.Add (optionalAddresses);

            root.UnevenRows = true;
            Root = root;
        }

        protected void ReloadAddresses ()
        {
            requiredAddresses = CustomSection (NcEmailAddress.Kind.Required);
            optionalAddresses = CustomSection (NcEmailAddress.Kind.Optional);

            for (int i = 0; i < AttendeeList.Count; i++) {
                var a = AttendeeList [i];
                var e = new StyledStringElement (a.DisplayName);
                e.Font = A.Font_AvenirNextRegular14;
                var lambda_object = i;
                e.Tapped += () => {
                    AddressTapped (lambda_object);
                };
                NcAssert.True (a.AttendeeTypeIsSet);
                switch (a.AttendeeType) {
                case NcAttendeeType.Required:
                    requiredAddresses.Add (e);
                    break;
                case NcAttendeeType.Optional:
                    optionalAddresses.Add (e);
                    break;
                case NcAttendeeType.Resource:
                case NcAttendeeType.Unknown:
                    // Skip, but preserve.
                    break;
                default:
                    NcAssert.CaseError ();
                    break;
                }
            }
        }

        /// <summary>
        /// Create a section for To, CC, and Bcc
        /// with special cell to trigger a new address.
        /// </summary>
        public Section CustomSection (NcEmailAddress.Kind kind)
        {
            var e = new StyledStringElement (NcEmailAddress.ToPrefix (kind));
            e.Image = UIImage.FromBundle ("icn-mtng-people");
            e.TextColor = UIColor.LightGray;
            e.BackgroundColor = UIColor.LightTextColor;
            e.Font = A.Font_AvenirNextRegular14;
            e.Tapped += () => {
                SectionTapped (kind);
            };
            var s = new Section ();
            s.HeaderView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 30.0f));
            s.FooterView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 1.0f));
            s.Add (e);
            return s;
        }

        protected void AddressTapped (int i)
        {
            var attendee = AttendeeList [i];
            var address = new NcEmailAddress (attendee);
            address.index = i;
            address.action = NcEmailAddress.Action.edit;
            PerformSegue ("AttendeeViewToContactChooser", new SegueHolder (address));
        }

        protected void SectionTapped (NcEmailAddress.Kind kind)
        {
            var address = new NcEmailAddress (kind);
            address.action = NcEmailAddress.Action.create;
            PerformSegue ("AttendeeViewToContactChooser", new SegueHolder (address));
        }

        /// <summary>
        /// Callback
        /// </summary>
        public void UpdateEmailAddress (NcEmailAddress address)
        {
            NcAssert.True (null != address);

            var mailboxAddress = address.ToMailboxAddress ();

            // FIXME: Deal with bad email address
            if (null == mailboxAddress) {
                return;
            }

            var name = mailboxAddress.Name;
            if (String.IsNullOrEmpty (name)) {
                name = mailboxAddress.Address;
            }

            switch (address.action) {
            case NcEmailAddress.Action.edit:
                AttendeeList [address.index].Name = name;
                AttendeeList [address.index].Email = mailboxAddress.Address;
                AttendeeList [address.index].AttendeeType = NcEmailAddress.ToAttendeeType (address.kind);
                AttendeeList [address.index].AttendeeTypeIsSet = true;
                break;
            case NcEmailAddress.Action.create:
                var attendee = new McAttendee ();
                attendee.Name = name;
                attendee.Email = mailboxAddress.Address;
                attendee.AttendeeType = NcEmailAddress.ToAttendeeType (address.kind);
                attendee.AttendeeTypeIsSet = true;
                AttendeeList.Add (attendee);
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
            NcAssert.True (null != address);

            if (NcEmailAddress.Action.edit == address.action) {
                AttendeeList.RemoveAt (address.index);
            }
        }
    }
}
