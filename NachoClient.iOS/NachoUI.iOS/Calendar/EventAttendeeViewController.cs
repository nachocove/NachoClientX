// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using System.Linq;
using System.Collections.Generic;
using Foundation;
using UIKit;
using EventKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public partial class EventAttendeeViewController : NcUIViewController, IAttendeeTableViewSourceDelegate, INachoContactChooserDelegate, INachoAttendeeListChooser, INachoContactDefaultSelector
    {

        protected AttendeeTableViewSource AttendeeSource;
        protected McAccount account;
        protected McAbstrCalendarRoot c;
        protected bool editing;
        protected bool organizer;
        protected bool recurring;
        protected string organizerName;
        protected string organizerEmail;

        UIBarButtonItem multiSelectButton;
        UIBarButtonItem multiResendButton;
        UIBarButtonItem multiRemoveButton;
        UIBarButtonItem multiCancelButton;
        UIBarButtonItem addAttendeesButton;
        public bool isMultiSelecting;

        protected INachoAttendeeListChooserDelegate owner;
        protected UIView organizerView;
        protected UISegmentedControl segmentedControl;
        protected UIView segmentedControlView;
        List<McAttendee> AttendeeList = new List<McAttendee> ();
        List<McAttendee> RequiredList = new List<McAttendee> ();
        List<McAttendee> OptionalList = new List<McAttendee> ();

        protected UITableView tableView;
        protected UILabel emptyListLabel;
        protected UILabel emptyMessagelabel;
        protected UIView addAttendeeView;

        protected static int SEGMENTED_CONTROL_TAG = 100;
        protected static readonly nfloat SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;

        public EventAttendeeViewController () : base ()
        {
        }

        public EventAttendeeViewController (IntPtr handle) : base (handle)
        {
        }

        public void Setup (INachoAttendeeListChooserDelegate owner, McAccount account, IList<McAttendee> attendees, McAbstrCalendarRoot c, bool editing, bool organizer, bool recurring)
        {
            this.owner = owner;
            this.account = account;
            this.AttendeeList = new List<McAttendee> (attendees);
            this.c = c;
            this.editing = editing;
            this.organizer = organizer;
            this.recurring = recurring;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            AttendeeSource = new AttendeeTableViewSource (this);

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

            PermissionManager.DealWithContactsPermission ();
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

        void ChooseAttendee (NcEmailAddress address)
        {
            var dc = new ContactChooserViewController ();
            dc.SetOwner (this, account, address, NachoContactType.EmailRequired);
            NavigationController.PushViewController (dc, true);
        }

        public void LoadAttendees ()
        {
            using (NcAbate.UIAbatement ()) {
                AttendeeSource.Setup (AttendeeList, account, editing, organizer, recurring);
                tableView.ReloadData ();
            }
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
            nfloat yOffset = 0;
            NavigationItem.Title = "Attendees";

            multiSelectButton = new NcUIBarButtonItem ();
            multiSelectButton.TintColor = A.Color_NachoBlue;
            multiSelectButton.Image = UIImage.FromBundle ("folder-edit");
            multiSelectButton.AccessibilityLabel = "Folder edit";
            multiSelectButton.Clicked += multiClicked;

            multiRemoveButton = new NcUIBarButtonItem ();
            multiRemoveButton.TintColor = A.Color_NachoBlue;
            multiRemoveButton.Image = UIImage.FromBundle ("gen-delete-all");
            multiRemoveButton.AccessibilityLabel = "Delete";
            multiRemoveButton.Clicked += removeClicked;

            multiResendButton = new NcUIBarButtonItem ();
            multiResendButton.TintColor = A.Color_NachoBlue;
            multiResendButton.Image = UIImage.FromBundle ("beer");
            multiResendButton.AccessibilityLabel = "Beer";
            multiResendButton.Clicked += resendClicked;

            multiCancelButton = new NcUIBarButtonItem ();
            multiCancelButton.TintColor = A.Color_NachoBlue;
            multiCancelButton.Image = UIImage.FromBundle ("gen-close");
            multiCancelButton.AccessibilityLabel = "Close";
            multiCancelButton.Clicked += cancelClicked;

            addAttendeesButton = new NcUIBarButtonItem ();
            addAttendeesButton.TintColor = A.Color_NachoBlue;
            addAttendeesButton.Image = UIImage.FromBundle ("calendar-add-attendee");
            addAttendeesButton.AccessibilityLabel = "Add attendee";

            addAttendeesButton.Clicked += (object sender, EventArgs e) => {
                var address = new NcEmailAddress (NcEmailAddress.Kind.Required);
                address.action = NcEmailAddress.Action.create;
                ChooseAttendee (address);
            }; 

            segmentedControlView = new UIView (new CGRect (0, yOffset, View.Frame.Width, 40));
            segmentedControlView.BackgroundColor = UIColor.White;

            segmentedControl = new UISegmentedControl ();
            segmentedControl.Frame = new CGRect (6, 5, View.Frame.Width - 12, 30);
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

            addAttendeeView = new UIView (new CGRect (A.Card_Horizontal_Indent, A.Card_Vertical_Indent + 40, View.Frame.Width - (A.Card_Horizontal_Indent * 2), View.Frame.Height - (2 * A.Card_Vertical_Indent) - 104));
            addAttendeeView.BackgroundColor = UIColor.White;
            addAttendeeView.Layer.CornerRadius = A.Card_Corner_Radius;
            addAttendeeView.Layer.BorderColor = A.Card_Border_Color;
            addAttendeeView.Layer.BorderWidth = A.Card_Border_Width;
            addAttendeeView.Hidden = true;
            View.AddSubview (addAttendeeView);

            emptyListLabel = new UILabel (new CGRect (0, 80, addAttendeeView.Frame.Width, 20));
            emptyListLabel.TextAlignment = UITextAlignment.Center;
            emptyListLabel.Font = A.Font_AvenirNextDemiBold14;
            emptyListLabel.TextColor = A.Color_NachoGreen;
            emptyListLabel.Text = "No attendees";
            emptyListLabel.Hidden = true;
            addAttendeeView.AddSubview (emptyListLabel);

            tableView = new UITableView (new CGRect (0, yOffset + 1, View.Frame.Width, View.Frame.Height - yOffset - 1 - 64), UITableViewStyle.Plain);
            tableView.SeparatorColor = UIColor.Clear;
            tableView.BackgroundColor = A.Color_NachoBackgroundGray;
            tableView.Source = AttendeeSource;
            tableView.AccessibilityLabel = "Event attendee";
            View.AddSubview (tableView);


            // When the user is adding attendees to an event and the 
            // list of attendees is empty they are presented with this message
            var stringAttributes = new UIStringAttributes {
                ForegroundColor = A.Color_NachoGreen,
                BackgroundColor = UIColor.White,
                Font = A.Font_AvenirNextDemiBold14
            };

            var noAttendeesString = new NSMutableAttributedString ("There are currently no attendees yet. \n \nStart adding attendees to your event by tapping on the  ", stringAttributes);
            var noAttendeesStringPartTwo = new NSAttributedString ("  icon above.", stringAttributes);

            var inlineIcon = new NachoInlineImageTextAttachment ();
            inlineIcon.Image = UIImage.FromBundle ("calendar-add-attendee-bottom");

            var stringWithImage = NSAttributedString.CreateFrom (inlineIcon);

            noAttendeesString.Append (stringWithImage);
            noAttendeesString.Append (noAttendeesStringPartTwo);

            var messageWidth = NMath.Max (addAttendeeView.Frame.Width - 4 * A.Card_Horizontal_Indent, 320 - 4 * A.Card_Horizontal_Indent);

            emptyMessagelabel = new UILabel (new CGRect (2 * A.Card_Horizontal_Indent, 20, messageWidth, 50));
            emptyMessagelabel.TextAlignment = UITextAlignment.Center;
            emptyMessagelabel.Lines = 0;
            emptyMessagelabel.LineBreakMode = UILineBreakMode.WordWrap;
            emptyMessagelabel.AttributedText = noAttendeesString;
            emptyMessagelabel.SizeToFit ();
            emptyMessagelabel.Center = new CGPoint (addAttendeeView.Frame.Width / 2, addAttendeeView.Frame.Height / 2); 
            emptyMessagelabel.Hidden = true;
            addAttendeeView.AddSubview (emptyMessagelabel);

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
                        segmentedControlView.Center = new CGPoint (segmentedControlView.Center.X, segmentedControlView.Center.Y - segmentedControlView.Frame.Height);
                        tableView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height);
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
                        segmentedControlView.Center = new CGPoint (segmentedControlView.Center.X, segmentedControlView.Center.Y + segmentedControlView.Frame.Height);
                        tableView.Frame = new CGRect (0, segmentedControlView.Frame.Height, View.Frame.Width, View.Frame.Height - segmentedControlView.Frame.Height);
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

        protected void ConfigureEventAttendeesView ()
        {
            segmentedControl.SelectedSegment = 0;
            if (0 == AttendeeList.Count) {
                tableView.Hidden = true;
                addAttendeeView.Hidden = false;
                emptyListLabel.Hidden = editing;
                emptyMessagelabel.Hidden = !editing;
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
                emptyListLabel.Hidden = editing;
                emptyMessagelabel.Hidden = !editing;
                emptyListLabel.Text = "No required attendees";
                emptyListLabel.SizeToFit ();
                emptyListLabel.Frame = new CGRect (0, 0, 320 - 2 * A.Card_Horizontal_Indent, addAttendeeView.Frame.Height - 64);
                emptyListLabel.Center = new CGPoint (addAttendeeView.Frame.Width / 2, addAttendeeView.Frame.Height / 2);
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
                emptyListLabel.Hidden = editing;
                emptyMessagelabel.Hidden = !editing;
                emptyListLabel.Text = "No optional attendees";
                emptyListLabel.SizeToFit ();
                emptyListLabel.Frame = new CGRect (0, 0, 320 - 2 * A.Card_Horizontal_Indent, addAttendeeView.Frame.Height - 64);
                emptyListLabel.Center = new CGPoint (addAttendeeView.Frame.Width / 2, addAttendeeView.Frame.Height / 2);
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

        public void AddLine (nfloat offset, nfloat yVal, nfloat width, UIColor color, UIView parentView)
        {
            var lineUIView = new UIView (new CGRect (offset, yVal, width, .5f));
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

            var mailboxAddress = address.ToMailboxAddress (mustUseAddress: true);

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
                // Get rid of any existing attendees with the same e-mail address.
                AttendeeList.RemoveAll((McAttendee a) => { return a.Email == mailboxAddress.Address; });
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
            // This is called when the user presses Enter in an empty search field.  There is nothing to delete.
        }

        // INachoContactChooser delegate
        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            vc.Cleanup ();
            NavigationController.PopToViewController (this, true);
        }

        public void EmailSwipeHandler (McContact contact)
        {
            if (contact == null) {
                Util.ComplainAbout ("No Email Address", "This contact does not have an email address.");
            } else {
                var address = Util.GetContactDefaultEmail (contact);
                if (address == null) {
                    if (contact.EmailAddresses.Count == 0) {
                        if (contact.CanUserEdit ()) {
                            SelectDefault (contact, ContactDefaultSelectionViewController.DefaultSelectionType.EmailAdder);
                        } else {
                            Util.ComplainAbout ("No Email Address", "This contact does not have an email address, and we are unable to modify the contact.");
                        }
                    } else {
                        SelectDefault (contact, ContactDefaultSelectionViewController.DefaultSelectionType.DefaultEmailSelector);
                    }
                } else {
                    ComposeMessage (address);
                }
            }
        }

        public void CallSwipeHandler (McContact contact)
        {
            Util.CallContact (contact, (ContactDefaultSelectionViewController.DefaultSelectionType type) => {
                SelectDefault(contact, type);
            });
        }

        void SelectDefault (McContact contact, ContactDefaultSelectionViewController.DefaultSelectionType type)
        {
            var destinationController = new ContactDefaultSelectionViewController ();
            destinationController.SetContact (contact);
            destinationController.viewType = type;
            destinationController.owner = this;
            PresentViewController (destinationController, true, null);
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
        public void SendAttendeeInvite (McAttendee attendee)
        {
            McCalendar item;
            if (c is McException) {
                item = McCalendar.QueryById<McCalendar> ((int)((McException)c).CalendarId);
                if (null == item) {
                    Log.Error (Log.LOG_UI, "EventAttendeeViewController.SendAttendeeInvite(): Can't find calendar event for exception.");
                    return;
                }
            } else if (c is McCalendar) {
                item = (McCalendar)c;
            } else {
                Log.Error (Log.LOG_UI, "EventAttendeeViewController.SendAttendeeInvite(): Calendar item is a {0} instead of a McCalendar.", c.GetType ().Name);
                return;
            }
            string plainTextDescription;
            switch (item.DescriptionType) {
            case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                plainTextDescription = Util.ConvertToPlainText (item.Description, NSDocumentType.HTML);
                break;
            case McAbstrFileDesc.BodyTypeEnum.RTF_3:
                plainTextDescription = Util.ConvertToPlainText (item.Description, NSDocumentType.RTF);
                break;
            default:
                plainTextDescription = item.Description;
                break;
            }
            var iCalPart = CalendarHelper.MimeRequestFromCalendar (item);
            var mimeBody = CalendarHelper.CreateMime (plainTextDescription, iCalPart, item.attachments);

            CalendarHelper.SendInvite (account, item, attendee, mimeBody);
        }

        public void SyncRequest ()
        {
            if (null == c) {
                Log.Error (Log.LOG_UI, "Internal error: EventAttendeeViewController.SyncRequest() was called with a null calendar item.");
                return;
            }
            if (0 == c.Id) {
                Log.Error (Log.LOG_UI, "Internal error: EventAttendeeViewController.SyncRequest() was called for a new event that hasn't been saved yet.");
                return;
            }
            if (!(c is McCalendar)) {
                Log.Error (Log.LOG_UI, "Internal error: EventAttendeeViewController.SyncRequest() was called with a {0} item instead of a McCalendar item.", c.GetType ().Name);
                return;
            }
            // For any changes to the attendees to be saved, McAbstrCalendarRoot.attendees needs to
            // be explicitly set.
            c.attendees = AttendeeList;
            c.Update ();
            BackEnd.Instance.UpdateCalCmd (account.Id, c.Id, false);
            c = McCalendar.QueryById<McCalendar> (c.Id);
        }


        /// IContactsTableViewSourceDelegate
        public void ContactSelectedCallback (McContact contact)
        {
            var contactViewController = new ContactDetailViewController ();
            contactViewController.contact = contact;
            NavigationController.PushViewController (contactViewController, true);
        }

        public void ContactDefaultSelectorComposeMessage (string address)
        {
            ComposeMessage (address);
        }

        private void ComposeMessage (string address)
        {
            var emailAccount = McAccount.EmailAccountForAccount (account);
            var message = McEmailMessage.MessageWithSubject (emailAccount, "");
            message.To = address;
            var composeViewController = new MessageComposeViewController (emailAccount);
            composeViewController.Composer.Message = message;
            composeViewController.Present ();
        }

    }

}