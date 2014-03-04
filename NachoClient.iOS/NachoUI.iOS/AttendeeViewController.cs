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
    public partial class AttendeeViewController : DialogViewController, INachoContactChooserDelegate
    {
        protected class MyEmailAddress : NcEmailAddress
        {
            public enum Action
            {
                undefined,
                create,
                edit,
            };

            public Action action;
            public int index;

            public MyEmailAddress (Kind kind) : base (kind)
            {
                this.action = Action.undefined;
            }

            public MyEmailAddress (Kind kind, string action) : base (kind, action)
            {
                this.action = Action.undefined;
            }

            public MyEmailAddress (McAttendee attendee) : base (attendee)
            {
                this.action = Action.undefined;
            }
        }

        List<McAttendee> AttendeeList = new List<McAttendee> ();

        public void SetAttendeeList (List<McAttendee> attendees)
        {
            this.AttendeeList = new List<McAttendee> ();
            foreach (var attendee in attendees) {
                this.AttendeeList.Add (attendee);
            }
        }

        public void GetAttendeeList (ref List<McAttendee> attendees)
        {
            attendees = new List<McAttendee> ();
            foreach (var attendee in this.AttendeeList) {
                attendees.Add (attendee);
            }
        }

        public AttendeeViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            doneButton.Clicked += (object sender, EventArgs e) => {
                NavigationController.PopViewControllerAnimated (true);
            };

            this.Pushing = true;
            this.Style = UITableViewStyle.Plain;
            TableView.SeparatorInset = new UIEdgeInsets (0, 0, 0, 0);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            ReloadRoot ();
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("AttendeeViewToContactChooser")) {
                var dc = (INachoContactChooser)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var address = (MyEmailAddress)holder.value;
                dc.SetOwner (this, address);
            }
        }

        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            NachoAssert.CaseError ();
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
                var e = new StringElement (a.DisplayName);
                var lambda_object = i;
                e.Tapped += () => {
                    AddressTapped (lambda_object);
                };
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
                    NachoAssert.CaseError ();
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
            e.Image = UIImage.FromBundle ("ic_action_add_person");
            e.TextColor = UIColor.LightGray;
            e.BackgroundColor = UIColor.LightTextColor;
            e.Tapped += () => {
                SectionTapped (kind);
            };
            var s = new Section ();
            s.HeaderView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 1.0f));
            s.FooterView = new UIView (new RectangleF (0.0f, 0.0f, 1.0f, 1.0f));
            s.Add (e);
            return s;
        }

        protected void AddressTapped (int i)
        {
            var attendee = AttendeeList [i];
            var address = new MyEmailAddress (attendee);
            address.index = i;
            address.action = MyEmailAddress.Action.edit;
            PerformSegue ("AttendeeViewToContactChooser", new SegueHolder (address));
        }

        protected void SectionTapped (NcEmailAddress.Kind kind)
        {
            var address = new MyEmailAddress (kind);
            address.action = MyEmailAddress.Action.create;
            PerformSegue ("AttendeeViewToContactChooser", new SegueHolder (address));
        }

        public void UpdateEmailAddress (NcEmailAddress address)
        {
            var a = address as MyEmailAddress;
            NachoAssert.True (null != a);

            switch (a.action) {
            case MyEmailAddress.Action.edit:
                break;
            case MyEmailAddress.Action.create:
                var attendee = new McAttendee ();
                if (null == a.contact) {
                    attendee.Name = address.address;
                    attendee.Email = address.address;
                } else {
                    attendee.Name = a.contact.DisplayName;
                    attendee.Email = a.contact.DisplayEmailAddress;
                }
                attendee.AttendeeType = NcEmailAddress.ToAttendeeType (a.kind);
                AttendeeList.Add (attendee);
                break;
            default:
                NachoAssert.CaseError ();
                break;
            }
        }

        public void DeleteEmailAddress (NcEmailAddress address)
        {
            var a = address as MyEmailAddress;
            NachoAssert.True (null != a);
            NachoAssert.True (MyEmailAddress.Action.edit == a.action);

            AttendeeList.RemoveAt (a.index);
        }
    }
}
