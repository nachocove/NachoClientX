// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.EventKit;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class EventAttendeeViewController : NcUIViewController, IAttendeeTableViewSourceDelegate, INachoContactChooserDelegate, INachoAttendeeListChooser
    {

        protected AttendeeTableViewSource attendeeSource;
        protected McAccount account;
        protected McCalendar c;
        protected bool editing;
        protected INachoAttendeeListChooserDelegate owner;
        protected UISegmentedControl segmentedControl;
        List<McAttendee> AttendeeList = new List<McAttendee> ();
        List<McAttendee> RequiredList = new List<McAttendee> ();
        List<McAttendee> OptionalList = new List<McAttendee> ();

        protected UILabel emptyListLabel;

        protected static int SEGMENTED_CONTROL_TAG = 100;
        protected static float SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;

        public EventAttendeeViewController (IntPtr handle) : base (handle)
        {
        }

        public void SetOwner (INachoAttendeeListChooserDelegate owner, List<McAttendee> attendees, McCalendar c, bool editing)
        {
            this.owner = owner;
            this.AttendeeList = attendees;
            this.c = c;
            this.editing = editing;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            EventAttendeesTableView.Frame = new RectangleF (0, 40, SCREEN_WIDTH, View.Frame.Height - 40);
            EventAttendeesTableView.SeparatorColor = A.Color_NachoBorderGray;

            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
            attendeeSource = new AttendeeTableViewSource ();
            attendeeSource.SetOwner (this);

            EventAttendeesTableView.Source = attendeeSource;

            EventAttendeesTableView.ReloadData ();
            CreateEventAttendeeView ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            LoadAttendees ();
            ConfigureEventAttendeesView ();
            UpdateLists ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            EventAttendeesTableView.ReloadData ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            owner.UpdateAttendeeList (this.AttendeeList);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("EventAttendeesToContactChooser")) {
                var dc = (INachoContactChooser)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var address = (NcEmailAddress)holder.value;
                dc.SetOwner (this, address, NachoContactType.EmailRequired);
                return;
            }

 
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        public void LoadAttendees ()
        {
            NachoClient.Util.HighPriority ();
            attendeeSource.SetAttendeeList (this.AttendeeList);
            attendeeSource.SetEditing (editing);
            attendeeSource.SetAccount (account);
            EventAttendeesTableView.ReloadData ();
            NachoClient.Util.RegularPriority ();
        }

        public void SetAttendeeList (List<McAttendee> attendees)
        {
            this.AttendeeList = new List<McAttendee> ();
            foreach (var attendee in attendees) {
                this.AttendeeList.Add (attendee);
            }
            UpdateLists ();
        }

        public List<McAttendee> GetAttendeeList ()
        {
            return this.AttendeeList;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_CalendarSetChanged == s.Status.SubKind) {
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback");
                EventAttendeesTableView.ReloadData ();
            }
        }

        protected void CreateEventAttendeeView ()
        {
            NavigationItem.Title = "Attendees";
            Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);

            if (editing) {
                NavigationItem.RightBarButtonItem = addAttendeeButton;
                addAttendeeButton.Clicked += (object sender, EventArgs e) => {
                    var address = new NcEmailAddress (NcEmailAddress.Kind.Required);
                    address.action = NcEmailAddress.Action.create;
                    PerformSegue ("EventAttendeesToContactChooser", new SegueHolder (address));
                };
            } else {
                NavigationItem.RightBarButtonItem = null;
            }
            emptyListLabel = new UILabel (new RectangleF (80, 80, 160, 20));
            emptyListLabel.TextAlignment = UITextAlignment.Center;
            emptyListLabel.Font = A.Font_AvenirNextDemiBold14;
            emptyListLabel.TextColor = A.Color_NachoBorderGray;
            emptyListLabel.Lines = 0;
            emptyListLabel.LineBreakMode = UILineBreakMode.WordWrap;
            emptyListLabel.Hidden = true;
            View.AddSubview (emptyListLabel);
           
            var segmentedControlView = new UIView (new RectangleF (0, 0, SCREEN_WIDTH, 40));
            segmentedControlView.BackgroundColor = UIColor.White;

            segmentedControl = new UISegmentedControl ();
            segmentedControl.Frame = new RectangleF (6, 5, View.Frame.Width - 12, 30);
            segmentedControl.InsertSegment ("All", 0, false);
            segmentedControl.InsertSegment ("Required", 1, false);
            segmentedControl.InsertSegment ("Optional", 2, false);
            segmentedControl.SelectedSegment = 0;
            segmentedControl.SelectedSegment = 0;
            segmentedControl.TintColor = A.Color_NachoIconGray;


            var segmentedControlTextAttributes = new UITextAttributes ();
            segmentedControlTextAttributes.Font = A.Font_AvenirNextRegular12;
            segmentedControl.SetTitleTextAttributes (segmentedControlTextAttributes, UIControlState.Normal);

            segmentedControl.ValueChanged += (sender, e) => {
                var selectedSegmentId = (sender as UISegmentedControl).SelectedSegment;
                switch (selectedSegmentId) {
                case 0:
                    ConfigureEventAttendeesView ();
                    break;
                case 1:
                    ConfigureRequiredList ();
                    break;
                case 2:
                    ConfigureOptionalList ();
                    break;
                default:
                    NcAssert.CaseError ();
                    break;
                }
            };

            AddLine (0, 40, SCREEN_WIDTH, A.Color_NachoBorderGray, segmentedControlView);
            segmentedControl.Tag = SEGMENTED_CONTROL_TAG;
            segmentedControlView.Add (segmentedControl);
            View.AddSubview (segmentedControlView);
        }

        string addMessage = "Add attendees with the \"+\" button";

        protected void ConfigureEventAttendeesView ()
        {
            segmentedControl.SelectedSegment = 0;
            if (0 == AttendeeList.Count) {
                EventAttendeesTableView.Hidden = true;
                emptyListLabel.Hidden = false;
                if (editing) {
                    emptyListLabel.Text = addMessage;
                    emptyListLabel.SizeToFit ();
                    emptyListLabel.Frame = new RectangleF (80, 80, 160, 40);
                } else {
                    emptyListLabel.Text = "No attendees";
                    emptyListLabel.SizeToFit ();
                    emptyListLabel.Frame = new RectangleF (80, 80, 160, 20);
                }
            } else {
                attendeeSource.SetAttendeeList (this.AttendeeList);
                EventAttendeesTableView.ReloadData ();
                EventAttendeesTableView.Hidden = false;
                emptyListLabel.Hidden = true;
            }
        }

        protected void ConfigureRequiredList ()
        {
            if (0 == RequiredList.Count) {
                EventAttendeesTableView.Hidden = true;
                emptyListLabel.Hidden = false;
                if (0 == AttendeeList.Count) {
                    if (editing) {
                        emptyListLabel.Text = addMessage;
                        emptyListLabel.SizeToFit ();
                        emptyListLabel.Frame = new RectangleF (80, 80, 160, 40);
                    } else {
                        emptyListLabel.Text = "No required attendees";
                        emptyListLabel.SizeToFit ();
                        emptyListLabel.Frame = new RectangleF (80, 80, 160, 20);
                    }
                } else {
                    emptyListLabel.Text = "No required attendees";
                    emptyListLabel.Frame = new RectangleF (0, 80, SCREEN_WIDTH, 20);
                }
            } else {
                attendeeSource.SetAttendeeList (this.RequiredList);
                EventAttendeesTableView.ReloadData ();
                EventAttendeesTableView.Hidden = false;
                emptyListLabel.Hidden = true;
            }
        }

        protected void ConfigureOptionalList ()
        {
            if (0 == OptionalList.Count) {
                EventAttendeesTableView.Hidden = true;
                emptyListLabel.Hidden = false;
                if (0 == AttendeeList.Count) {
                    if (editing) {
                        emptyListLabel.Text = addMessage;
                        emptyListLabel.SizeToFit ();
                        emptyListLabel.Frame = new RectangleF (80, 80, 160, 40);
                    } else {
                        emptyListLabel.Text = "No optional attendees";
                        emptyListLabel.SizeToFit ();
                        emptyListLabel.Frame = new RectangleF (80, 80, 160, 20);
                    }
                } else {
                    emptyListLabel.Text = "No optional attendees";
                    emptyListLabel.Frame = new RectangleF (0, 80, SCREEN_WIDTH, 20);
                }
            } else {
                attendeeSource.SetAttendeeList (this.OptionalList);
                EventAttendeesTableView.ReloadData ();
                EventAttendeesTableView.Hidden = false;
                emptyListLabel.Hidden = true;
            }
        }

        public void ConfigureAttendeeTable ()
        {
            if (0 == segmentedControl.SelectedSegment) {
                ConfigureEventAttendeesView ();
            } else if (1 == segmentedControl.SelectedSegment) {
                ConfigureRequiredList ();
            } else if (2 == segmentedControl.SelectedSegment) {
                ConfigureOptionalList ();
            }
        }

        public void AddLine (float offset, float yVal, float width, UIColor color, UIView parentView)
        {
            var lineUIView = new UIView (new RectangleF (offset, yVal, width, .5f));
            lineUIView.BackgroundColor = color;
            parentView.Add (lineUIView);
        }

        public void RemoveAttendee (McAttendee attendee)
        {
            List<McAttendee> tempList = new List<McAttendee> ();
            foreach (var a in AttendeeList) {
                if (a.Email != attendee.Email) {
                    tempList.Add (a);
                }
            }
            AttendeeList = tempList;
            ConfigureEventAttendeesView ();
            UpdateLists ();
        }

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
                attendee.AccountId = account.Id;
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
            UpdateLists ();
        }

        public void DeleteEmailAddress (NcEmailAddress address)
        {
            NcAssert.CaseError ();
        }

        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            NcAssert.CaseError ();
        }

        public void UpdateLists ()
        {
            this.RequiredList.Clear ();
            this.OptionalList.Clear ();

            foreach (var attendee in AttendeeList) {
                if (attendee.AttendeeType == NcAttendeeType.Required) {
                    this.RequiredList.Add (attendee);
                }
                if (attendee.AttendeeType == NcAttendeeType.Optional) {
                    this.OptionalList.Add (attendee);
                }
            }
        }

        public Int64 GetAccountId ()
        {
            return account.Id;
        }

        /// IContactsTableViewSourceDelegate
        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
        }

        /// IContactsTableViewSourceDelegate
        public void SendAttendeeInvite (McAttendee attendee)
        {
            NcAssert.CaseError ();
        }


        /// IContactsTableViewSourceDelegate
        public void ContactSelectedCallback (McContact contact)
        {
            PerformSegue ("ContactsToContactDetail", new SegueHolder (contact));
        }

    }

}
