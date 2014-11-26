// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.EventKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class EventAttendeeViewController : NcUIViewController, IAttendeeTableViewSourceDelegate, INachoContactChooserDelegate, INachoAttendeeListChooser
    {

        protected AttendeeTableViewSource AttendeeSource;
        protected McAccount account;
        protected McAbstrCalendarRoot c;
        protected bool editing;

        UIBarButtonItem multiSelectButton;
        UIBarButtonItem multiResendButton;
        UIBarButtonItem multiRemoveButton;
        UIBarButtonItem multiCancelButton;
        UIBarButtonItem addAttendeesButton;
        public bool isMultiSelecting;

        protected INachoAttendeeListChooserDelegate owner;
        protected UISegmentedControl segmentedControl;
        protected UIView segmentedControlView;
        List<McAttendee> AttendeeList = new List<McAttendee> ();
        List<McAttendee> RequiredList = new List<McAttendee> ();
        List<McAttendee> OptionalList = new List<McAttendee> ();

        protected UITableView tableView;
        protected UILabel emptyListLabel;
        protected UIView addAttendeeView;
        protected UIImageView iconIv;

        protected static int SEGMENTED_CONTROL_TAG = 100;
        protected static float SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;

        public EventAttendeeViewController (IntPtr handle) : base (handle)
        {
        }

        public void SetOwner (INachoAttendeeListChooserDelegate owner, List<McAttendee> attendees, McAbstrCalendarRoot c, bool editing)
        {
            this.owner = owner;
            this.AttendeeList = attendees;
            this.c = c;
            this.editing = editing;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            //TODO remove from storyboard
            EventAttendeesTableView.Hidden = true;

            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
            AttendeeSource = new AttendeeTableViewSource (this, this);

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
            tableView.ReloadData ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (editing && null != owner) {
                owner.UpdateAttendeeList (this.AttendeeList);
            }
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
            NachoCore.Utils.NcAbate.HighPriority ("EventAttendeeViewController LoadAttendees");
            AttendeeSource.SetAttendeeList (this.AttendeeList);
            AttendeeSource.SetEditing (editing);
            AttendeeSource.SetAccount (account);
            tableView.ReloadData ();
            NachoCore.Utils.NcAbate.RegularPriority ("EventAttendeeViewController LoadAttendees");
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
                tableView.ReloadData ();
            }
        }

        protected void CreateEventAttendeeView ()
        {
            var yOffset = 0f;
            NavigationItem.Title = "Attendees";

            multiSelectButton = new UIBarButtonItem ();
            multiSelectButton.TintColor = A.Color_NachoBlue;
            multiSelectButton.Image = UIImage.FromBundle ("folder-edit");
            multiSelectButton.Clicked += multiClicked;

            multiRemoveButton = new UIBarButtonItem ();
            multiRemoveButton.TintColor = A.Color_NachoBlue;
            multiRemoveButton.Image = UIImage.FromBundle ("gen-delete-all");
            multiRemoveButton.Clicked += removeClicked;

            multiResendButton = new UIBarButtonItem ();
            multiResendButton.TintColor = A.Color_NachoBlue;
            multiResendButton.Image = UIImage.FromBundle ("beer");
            multiResendButton.Clicked += resendClicked;

            multiCancelButton = new UIBarButtonItem ();
            multiCancelButton.TintColor = A.Color_NachoBlue;
            multiCancelButton.Image = UIImage.FromBundle ("gen-close");
            multiCancelButton.Clicked += cancelClicked;

            addAttendeesButton = new UIBarButtonItem ();
            addAttendeesButton.TintColor = A.Color_NachoBlue;
            addAttendeesButton.Image = UIImage.FromBundle ("calendar-add-attendee");

            addAttendeesButton.Clicked += (object sender, EventArgs e) => {
                var address = new NcEmailAddress (NcEmailAddress.Kind.Required);
                address.action = NcEmailAddress.Action.create;
                PerformSegue ("EventAttendeesToContactChooser", new SegueHolder (address));
            };

            segmentedControlView = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 40));
            segmentedControlView.BackgroundColor = UIColor.White;

            segmentedControl = new UISegmentedControl ();
            segmentedControl.Frame = new RectangleF (6, yOffset + 5, View.Frame.Width - 12, 30);
            segmentedControl.InsertSegment ("All", 0, false);
            segmentedControl.InsertSegment ("Required", 1, false);
            segmentedControl.InsertSegment ("Optional", 2, false);
            segmentedControl.SelectedSegment = 0;
            segmentedControl.TintColor = A.Color_NachoGreen;

            var segmentedControlTextAttributes = new UITextAttributes ();
            segmentedControlTextAttributes.Font = A.Font_AvenirNextDemiBold14;
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
            yOffset += segmentedControlView.Frame.Height;
            AddLine (0, yOffset, SCREEN_WIDTH, A.Color_NachoBorderGray, segmentedControlView);
            segmentedControl.Tag = SEGMENTED_CONTROL_TAG;
            segmentedControlView.Add (segmentedControl);
            View.AddSubview (segmentedControlView);

            addAttendeeView = new UIView (new RectangleF (A.Card_Horizontal_Indent, A.Card_Vertical_Indent + 40, View.Frame.Width - (A.Card_Horizontal_Indent * 2), View.Frame.Height - (2 * A.Card_Vertical_Indent) - 104));
            addAttendeeView.BackgroundColor = UIColor.White;
            addAttendeeView.Layer.CornerRadius = A.Card_Corner_Radius;
            addAttendeeView.Layer.BorderColor = A.Card_Border_Color;
            addAttendeeView.Layer.BorderWidth = A.Card_Border_Width;
            addAttendeeView.Hidden = true;
            View.AddSubview (addAttendeeView);

            emptyListLabel = new UILabel (new RectangleF (0, 80, addAttendeeView.Frame.Width, 20));
            emptyListLabel.TextAlignment = UITextAlignment.Center;
            emptyListLabel.Font = A.Font_AvenirNextDemiBold14;
            emptyListLabel.TextColor = A.Color_NachoGreen;
            emptyListLabel.Lines = 0;
            emptyListLabel.LineBreakMode = UILineBreakMode.WordWrap;
            addAttendeeView.AddSubview (emptyListLabel);

            iconIv = new UIImageView (new RectangleF(0, 0, 16, 16));
            iconIv.Image = UIImage.FromBundle ("calendar-add-attendee-bottom");
            addAttendeeView.AddSubview (iconIv);

            tableView = new UITableView (new RectangleF (0, 41, View.Frame.Width, View.Frame.Height - 40), UITableViewStyle.Plain);
            tableView.SeparatorColor = UIColor.Clear;
            tableView.BackgroundColor = A.Color_NachoBackgroundGray;
            tableView.Source = AttendeeSource;
            View.AddSubview (tableView);

            View.BackgroundColor = A.Color_NachoBackgroundGray;
        }

        private void multiClicked (object sender, EventArgs e)
        {
            ToggleMultiSelect (true);
        }

        private void removeClicked (object sender, EventArgs e)
        {
            foreach (var item in AttendeeSource.MultiSelect) {
                AttendeeSource.RemoveAttendee (item.Value);
            }
            EndMultiSelect ();
        }

        private void resendClicked (object sender, EventArgs e)
        {
            EndMultiSelect ();
        }

        private void cancelClicked (object sender, EventArgs e)
        {
            EndMultiSelect ();
        }

        private void EndMultiSelect ()
        {
            AttendeeSource.MultiSelect.Clear ();
            ToggleMultiSelect (false);
        }

        private void ToggleMultiSelect (bool isMultiSelect)
        {
            if (isMultiSelect) {
                NavigationItem.Title = "";
                isMultiSelecting = true;
                AttendeeSource.IsMultiSelecting = true;
                ConfigureNavBar (0);
                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        segmentedControlView.Center = new PointF (segmentedControlView.Center.X, segmentedControlView.Center.Y - segmentedControlView.Frame.Height);
                        tableView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height);
                        ConfigureVisibleCells ();
                    },
                    () => {
                    }
                );

            } else {
                NavigationItem.Title = "Attendees";
                isMultiSelecting = false;
                AttendeeSource.IsMultiSelecting = false;
                ConfigureNavBar (0);
                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        segmentedControlView.Center = new PointF (segmentedControlView.Center.X, segmentedControlView.Center.Y + segmentedControlView.Frame.Height);
                        tableView.Frame = new RectangleF (0, segmentedControlView.Frame.Height, View.Frame.Width, View.Frame.Height - segmentedControlView.Frame.Height);
                        ConfigureVisibleCells ();
                    },
                    () => {

                    }
                );
            }
        }

        public void ConfigureVisibleCells ()
        {
            foreach (var path in tableView.IndexPathsForVisibleRows) {
                AttendeeSource.ConfigureCell (tableView, tableView.CellAt (path), path);
            }
        }

        public void ConfigureNavBar (int multiCount)
        {
            NavigationItem.LeftBarButtonItem = null;
            NavigationItem.RightBarButtonItem = null;

            if (editing) {
                if (0 != AttendeeList.Count) {
                    if (isMultiSelecting) {
                        NavigationItem.LeftBarButtonItem = multiCancelButton;
                        NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                            multiRemoveButton,
                            multiResendButton
                        };
                        if (0 == multiCount) {
                            multiRemoveButton.Enabled = false;
                            multiResendButton.Enabled = false;
                        } else {
                            multiRemoveButton.Enabled = true;
                            multiResendButton.Enabled = true;
                        }
                    } else {
                        Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);
                        NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                            //multiSelectButton,
                            addAttendeesButton
                        };
                    }
                } else {
                    Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);
                    NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                        addAttendeesButton
                    };
                }
            } else {
                Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);
            }
        }

        string addMessage = "There are currently no attendees yet. \n \n Start adding attendees to your event by tapping on the        icon above.";

        protected void ConfigureEventAttendeesView ()
        {
            segmentedControl.SelectedSegment = 0;
            if (0 == AttendeeList.Count) {
                tableView.Hidden = true;
                addAttendeeView.Hidden = false;
                if (editing) {
                    emptyListLabel.Text = addMessage;
                } else {
                    emptyListLabel.Text = "No attendees";
                }
                emptyListLabel.SizeToFit ();
                emptyListLabel.Frame = new RectangleF (0, 0, addAttendeeView.Frame.Width, addAttendeeView.Frame.Height - 64);
                iconIv.Frame = new RectangleF (emptyListLabel.Center.X + 4, emptyListLabel.Center.Y + 21, 16, 16);
            } else {
                AttendeeSource.SetAttendeeList (this.AttendeeList);
                tableView.ReloadData ();
                tableView.Hidden = false;
                addAttendeeView.Hidden = true;
            }

            ConfigureNavBar (0);
        }

        protected void ConfigureRequiredList ()
        {
            if (0 == RequiredList.Count) {
                tableView.Hidden = true;
                addAttendeeView.Hidden = false;
                if (0 == AttendeeList.Count) {
                    if (editing) {
                        emptyListLabel.Text = addMessage;
                    } else {
                        emptyListLabel.Text = "No required attendees";
                    }
                } else {
                    emptyListLabel.Text = "No required attendees";
                }
                emptyListLabel.SizeToFit ();
                emptyListLabel.Frame = new RectangleF (0, 0, addAttendeeView.Frame.Width, addAttendeeView.Frame.Height - 64);
            } else {
                AttendeeSource.SetAttendeeList (this.RequiredList);
                tableView.ReloadData ();
                tableView.Hidden = false;
                addAttendeeView.Hidden = true;
            }
        }

        protected void ConfigureOptionalList ()
        {
            if (0 == OptionalList.Count) {
                tableView.Hidden = true;
                addAttendeeView.Hidden = false;
                if (0 == AttendeeList.Count) {
                    if (editing) {
                        emptyListLabel.Text = addMessage;
                    } else {
                        emptyListLabel.Text = "No optional attendees";
                    }
                } else {
                    emptyListLabel.Text = "No optional attendees";
                }
                emptyListLabel.SizeToFit ();
                emptyListLabel.Frame = new RectangleF (0, 0, addAttendeeView.Frame.Width, addAttendeeView.Frame.Height - 64);
            } else {
                AttendeeSource.SetAttendeeList (this.OptionalList);
                tableView.ReloadData ();
                tableView.Hidden = false;
                addAttendeeView.Hidden = true;
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

        // INachoContactChooser delegate
        public void UpdateEmailAddress (INachoContactChooser vc, NcEmailAddress address)
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

        // INachoContactChooser delegate
        public void DeleteEmailAddress (INachoContactChooser vc, NcEmailAddress address)
        {
            NcAssert.CaseError ();
        }

        // INachoContactChooser delegate
        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            vc.Cleanup ();
            NavigationController.PopToViewController (this, true);
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
//            var iCalPart = CalendarHelper.iCalToMimePart (account, c);
//            var mimeBody = CalendarHelper.CreateMime (c.Description, iCalPart, c.attachments);
//
//            CalendarHelper.SendInvite (account, c, attendee, mimeBody);
        }


        /// IContactsTableViewSourceDelegate
        public void ContactSelectedCallback (McContact contact)
        {
            PerformSegue ("ContactsToContactDetail", new SegueHolder (contact));
        }

    }

}
