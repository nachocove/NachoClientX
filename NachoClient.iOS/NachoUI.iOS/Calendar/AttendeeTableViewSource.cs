//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using CoreGraphics;
using UIKit;
using Foundation;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class AttendeeTableViewSource : UITableViewSource
    {
        List<McAttendee> AttendeeList = new List<McAttendee> ();
        protected McAccount Account;
        protected bool editing = false;
        protected bool organizer = false;
        protected bool recurring = false;
        public IAttendeeTableViewSourceDelegate owner;
        protected bool isMultiSelecting;
        protected Dictionary<NSIndexPath,McAttendee> multiSelect = null;

        const string AttendeeCell = "AttendeeCell";

        protected UIColor CELL_COMPONENT_BG_COLOR = UIColor.White;

        protected const int SWIPE_TAG = 99100;
        protected const int CELL_VIEW_TAG = 99200;
        protected const int MULTI_SELECT_CELL_VIEW_TAG = 99300;

        protected static int MULTI_ICON_TAG = 175;
        protected static int SEPARATOR_LINE_TAG = 600;

        private const int RESEND_INVITE_TAG = 1000;
        private const int MAKE_REQUIRED_TAG = 2000;
        private const int MAKE_OPTIONAL_TAG = 3000;

        private const int DELETE_TAG = 4000;

        private const int CALL_SWIPE_TAG = 5000;
        private const int EMAIL_SWIPE_TAG = 6000;

        protected const string AttendeeCellReuseIdentifier = "AttendeeCell";

        // Pre-made swipe action descriptors
        private static SwipeActionDescriptor RESEND_INVITE_BUTTON =
            new SwipeActionDescriptor (RESEND_INVITE_TAG, 0.25f, UIImage.FromBundle ("files-forward-swipe"),
                "Send Invite", A.Color_NachoeSwipeForward);
        private static SwipeActionDescriptor MAKE_REQUIRED_BUTTON =
            new SwipeActionDescriptor (MAKE_REQUIRED_TAG, 0.25f, UIImage.FromBundle ("calendar-attendee-required-swipe"),
                "Required", A.Color_NachoSwipeDialIn);
        private static SwipeActionDescriptor MAKE_OPTIONAL_BUTTON =
            new SwipeActionDescriptor (MAKE_OPTIONAL_TAG, 0.25f, UIImage.FromBundle ("calendar-attendee-optional-swipe"),
                "Optional", A.Color_NachoSwipeEmailMove);
        private static SwipeActionDescriptor DELETE_BUTTON =
            new SwipeActionDescriptor (DELETE_TAG, 0.25f, UIImage.FromBundle ("email-delete-swipe"),
                "Remove", A.Color_NachoSwipeActionRed);

        private static SwipeActionDescriptor CALL_BUTTON =
            new SwipeActionDescriptor (CALL_SWIPE_TAG, 0.25f, UIImage.FromBundle ("contacts-call-swipe"),
                "Dial", A.Color_NachoSwipeActionOrange);
        private static SwipeActionDescriptor EMAIL_BUTTON =
            new SwipeActionDescriptor (EMAIL_SWIPE_TAG, 0.25f, UIImage.FromBundle ("contacts-email-swipe"),
                "Email", A.Color_NachoSwipeActionMatteBlack);

        public Dictionary<NSIndexPath, McAttendee> MultiSelect {
            get { return multiSelect; }
            set { multiSelect = value; }
        }

        public bool IsMultiSelecting {
            get { return isMultiSelecting; }
            set { isMultiSelecting = value; }
        }

        public AttendeeTableViewSource (IAttendeeTableViewSourceDelegate owner)
        {
            this.multiSelect = new Dictionary<NSIndexPath,McAttendee> ();
            this.owner = owner;
        }

        public void Setup (IList<McAttendee> attendees, McAccount account, bool editing, bool organizer, bool recurring)
        {
            SetAttendeeList (attendees);
            this.Account = account;
            this.editing = editing;
            this.organizer = organizer;
            this.recurring = recurring;
        }

        public void SetAttendeeList (IList<McAttendee> attendees)
        {
            this.AttendeeList = new List<McAttendee> (attendees);
        }

        public List<McAttendee> GetAttendeeList ()
        {
            return this.AttendeeList;
        }

        /// <summary>
        /// Tableview delegate
        /// </summary>
        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        /// <summary>
        /// The number of rows in the specified section.
        /// </summary>
        public override nint RowsInSection (UITableView tableview, nint section)
        {
            return AttendeeList.Count;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            var attendee = AttendeeList [indexPath.Row];
            McContact contact = McContact.QueryByEmailAddress (Account.Id, attendee.Email).FirstOrDefault ();
            if (null == contact) {
                NcContactGleaner.GleanContacts (attendee.Email, Account.Id, false);
                contact = McContact.QueryByEmailAddress (Account.Id, attendee.Email).FirstOrDefault ();
            }
            owner.ContactSelectedCallback (contact);
        }

        UIView ViewWithImageName (string imageName)
        {
            var image = UIImage.FromBundle (imageName);
            var imageView = new UIImageView (image);
            imageView.ContentMode = UIViewContentMode.Center;
            return imageView;
        }

        UIView ViewWithLabel (string text, string side)
        {
            var label = new UILabel ();
            label.Text = text;
            label.Font = A.Font_AvenirNextDemiBold14;
            label.TextColor = UIColor.White;
            //label.TextAlignment = ta;
            label.SizeToFit ();
            var labelView = new UIView ();
            if ("left" == side) {
                labelView.Frame = new CGRect (0, 0, label.Frame.Width + 50, label.Frame.Height);
            } else {
                labelView.Frame = new CGRect (65, 0, label.Frame.Width + 50, label.Frame.Height);
            }
            labelView.Add (label);
            return labelView;
        }

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return 80;
        }

        protected const int USER_NAME_TAG = 333;
        protected const int USER_LABEL_TAG = 334;
        protected const int USER_EMAIL_TAG = 335;
        protected const int USER_RESPONSE_TAG = 336;
        protected const int USER_RESPONSE_VIEW_TAG = 337;
        protected const int USER_IMAGE_TAG = 338;

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = null;
            cell = tableView.DequeueReusableCell (AttendeeCell);
            if (null == cell) {
                cell = CreateCell (tableView, AttendeeCell);
            }
            NcAssert.True (null != cell);

            ConfigureCell (tableView, cell, indexPath);

            return cell;
        }

        protected UITableViewCell CreateCell (UITableView tableView, string identifier)
        {
            var cell = tableView.DequeueReusableCell (identifier);
            if (null == cell) {
                cell = new UITableViewCell (UITableViewCellStyle.Default, identifier);
            }
            if (cell.RespondsToSelector (new ObjCRuntime.Selector ("setSeparatorInset:"))) {
                cell.SeparatorInset = UIEdgeInsets.Zero;
            }
            cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
            cell.ContentView.BackgroundColor = CELL_COMPONENT_BG_COLOR;

            var cellWidth = tableView.Frame.Width;

            var frame = new CGRect (0, 0, tableView.Frame.Width, 80);
            var view = new SwipeActionView (frame);

            cell.AddSubview (view);
            view.Tag = SWIPE_TAG;
            view.BackgroundColor = CELL_COMPONENT_BG_COLOR;

            //Multi select icon
            var multiSelectImageView = new UIImageView ();
            multiSelectImageView.Tag = MULTI_ICON_TAG;
            multiSelectImageView.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            multiSelectImageView.Frame = new CGRect (18, (view.Frame.Height / 2) - 8, 16, 16);
            multiSelectImageView.Hidden = true;
            view.AddSubview (multiSelectImageView);

            //User Name
            var userNameLabel = new UILabel (new CGRect (70, 22 - 2.5f, cellWidth - 15 - 65, 20));
            userNameLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            userNameLabel.Tag = USER_NAME_TAG;
            view.AddSubview (userNameLabel);

            //User Email
            var userEmailView = new UILabel (new CGRect (70, 40 - 2.5f, cellWidth - 15 - 65, 20));
            userEmailView.LineBreakMode = UILineBreakMode.TailTruncation;
            userEmailView.Tag = USER_EMAIL_TAG;
            view.AddSubview (userEmailView);

            // User image
            var userImageView = new UIImageView (new CGRect (15, 15 - 2.5f, 45, 45));
            userImageView.Layer.CornerRadius = (45.0f / 2.0f);
            userImageView.Layer.MasksToBounds = true;
            userImageView.Tag = USER_IMAGE_TAG;
            view.AddSubview (userImageView);

            // User userLabelView view, if no image
            var userLabelView = new UILabel (new CGRect (15, 15 - 2.5f, 45, 45));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = CELL_COMPONENT_BG_COLOR;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = (45 / 2);
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Tag = USER_LABEL_TAG;
            view.AddSubview (userLabelView);

            //User response indicator
            var attendeeResponseView = new UIView (new CGRect (15 + 27, 42 - 2.5f, 20, 20));
            attendeeResponseView.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            attendeeResponseView.Layer.CornerRadius = 10;
            attendeeResponseView.Tag = USER_RESPONSE_VIEW_TAG;
            MakeEmptyCircle (attendeeResponseView);
            UIImageView responseImageView = new UIImageView (new CGRect (2.5f, 2.5f, 15, 15));
                
            responseImageView.Tag = USER_RESPONSE_TAG;
            attendeeResponseView.Add (responseImageView);
            view.AddSubview (attendeeResponseView);

            //Separator line
            var separatorLine = Util.AddHorizontalLine (70, 80, cell.Frame.Width - 60, A.Color_NachoBorderGray);
            separatorLine.Tag = SEPARATOR_LINE_TAG;
            view.AddSubview (separatorLine);

            cell.AddSubview (view);
            return cell;
        }

        public void ConfigureCell (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            nfloat xOffset = isMultiSelecting ? 34 : 0;

            //Attendee
            McAttendee attendee = AttendeeList [indexPath.Row];

            var displayName = attendee.DisplayName;
            var displayEmailAddress = attendee.Email;

            int colorIndex = 1;

            if (!String.IsNullOrEmpty (displayEmailAddress)) {
                McEmailAddress emailAddress;
                if (McEmailAddress.Get (Account.Id, displayEmailAddress, out emailAddress)) {
                    displayEmailAddress = emailAddress.CanonicalEmailAddress;
                    colorIndex = emailAddress.ColorIndex;
                }
            }

            //Swipe view
            var view = cell.ViewWithTag (SWIPE_TAG) as SwipeActionView;
            view.ClearActions (SwipeSide.LEFT);
            view.ClearActions (SwipeSide.RIGHT);
            if (organizer) {
                if (!editing) {
                    view.SetAction (EMAIL_BUTTON, SwipeSide.RIGHT);
                    view.SetAction (CALL_BUTTON, SwipeSide.LEFT);
                }
                if (!recurring) {
                    if (NcAttendeeType.Required == attendee.AttendeeType) {
                        view.SetAction (MAKE_OPTIONAL_BUTTON, SwipeSide.RIGHT);
                    } else {
                        view.SetAction (MAKE_REQUIRED_BUTTON, SwipeSide.RIGHT);
                    }
                    if (editing) {
                        view.SetAction (DELETE_BUTTON, SwipeSide.RIGHT);
                    }
                }
                view.SetAction (RESEND_INVITE_BUTTON, SwipeSide.LEFT);

                if (isMultiSelecting) {
                    view.DisableSwipe ();
                } else {
                    view.EnableSwipe ();
                }

                view.OnClick = (int tag) => {
                    switch (tag) {
                    case MAKE_REQUIRED_TAG:
                        ChangeAttendeeType (cell, attendee);
                        break;
                    case MAKE_OPTIONAL_TAG:
                        ChangeAttendeeType (cell, attendee);
                        break;
                    case RESEND_INVITE_TAG:
                        ResendInvite (attendee);
                        break;
                    case DELETE_TAG:
                        RemoveAttendee (attendee);
                        break;
                    case CALL_SWIPE_TAG:
                        CallSwipeHandler (attendee);
                        break;
                    case EMAIL_SWIPE_TAG:
                        EmailSwipeHandler (attendee);
                        break;
                    default:
                        throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action tag {0}", tag));
                    }
                };

            } else {

                view.SetAction (CALL_BUTTON, SwipeSide.LEFT);
                //view.SetAction (RESEND_INVITE_BUTTON, SwipeSide.LEFT);
                view.SetAction (EMAIL_BUTTON, SwipeSide.RIGHT);

                view.OnClick = (int tag) => {
                    switch (tag) {
                    case CALL_SWIPE_TAG:
                        CallSwipeHandler (attendee);
                        break;
                    case EMAIL_SWIPE_TAG:
                        EmailSwipeHandler (attendee);
                        break;
//                    case RESEND_INVITE_TAG:
//                        ResendInvite (attendee);
//                        break;
                    default:
                        throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action tag {0}", tag));
                    }
                };
            }
            view.OnSwipe = (SwipeActionView actionView, SwipeActionView.SwipeState state) => {
                switch (state) {
                case SwipeActionView.SwipeState.SWIPE_BEGIN:
                    tableView.ScrollEnabled = false;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    tableView.ScrollEnabled = true;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    tableView.ScrollEnabled = false;
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown swipe state {0}", (int)state));
                }
            };

            //Multiselect icon
            var multiSelectImageView = view.ViewWithTag (MULTI_ICON_TAG) as UIImageView;
            multiSelectImageView.Hidden = isMultiSelecting ? false : true;
            SetMultiSelectIcon (multiSelectImageView, indexPath);

            var textLabel = cell.ViewWithTag (USER_NAME_TAG) as UILabel;
            textLabel.Frame = new CGRect (70 + xOffset, 22 - 2.5f, tableView.Frame.Width - 80 - xOffset, 20);

            var detailTextLabel = cell.ViewWithTag (USER_EMAIL_TAG) as UILabel;
            detailTextLabel.Frame = new CGRect (70 + xOffset, 40 - 2.5f, tableView.Frame.Width - 80 - xOffset, 20);

            var labelView = cell.ViewWithTag (USER_LABEL_TAG) as UILabel;
            labelView.Frame = new CGRect (15 + xOffset, 15 - 2.5f, 45, 45);

            var imageView = cell.ViewWithTag (USER_IMAGE_TAG) as UIImageView;
            imageView.Frame = new CGRect (15 + xOffset, 15 - 2.5f, 45, 45);
            bool hasImage = false;

            var responseCircle = cell.ViewWithTag (USER_RESPONSE_VIEW_TAG) as UIView;
            responseCircle.Frame = new CGRect (15 + 27 + xOffset, 42 - 2.5f, 20, 20);

            using (UIImage image = Util.ImageOfSender ((int)owner.GetAccountId (), attendee.Email)) {
                if (null != image) {
                    imageView.Image = image;
                    imageView.Layer.BorderWidth = .25f;
                    imageView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
                    hasImage = true;
                }
            }

            // Both empty
            if (String.IsNullOrEmpty (displayName) && String.IsNullOrEmpty (displayEmailAddress)) {
                textLabel.Text = "Contact has no name or email address";
                textLabel.TextColor = UIColor.LightGray;
                textLabel.Font = A.Font_AvenirNextRegular14;
                labelView.Hidden = true;
                return;
            }

            // Name empty
            if (String.IsNullOrEmpty (displayName)) {
                textLabel.Text = displayEmailAddress;
                textLabel.TextColor = A.Color_0B3239;
                textLabel.Font = A.Font_AvenirNextDemiBold17;
                labelView.Hidden = hasImage;
                labelView.Text = ContactsHelper.NameToLetters (displayEmailAddress);
                labelView.BackgroundColor = Util.ColorForUser (colorIndex);
                return;
            }

            // Email empty
            if (String.IsNullOrEmpty (displayEmailAddress)) {
                textLabel.Text = displayName;
                detailTextLabel.Text = "Contact has no email address";
                textLabel.TextColor = A.Color_0B3239;
                textLabel.Font = A.Font_AvenirNextDemiBold17;
                detailTextLabel.TextColor = UIColor.LightGray;
                detailTextLabel.Font = A.Font_AvenirNextRegular12;
                labelView.Hidden = hasImage;
                labelView.Text = ContactsHelper.NameToLetters (displayName);
                labelView.BackgroundColor = Util.ColorForUser (colorIndex);
                return;
            }

            // Everything
            textLabel.Text = displayName;
            detailTextLabel.Text = displayEmailAddress;
            textLabel.TextColor = A.Color_0B3239;
            textLabel.Font = A.Font_AvenirNextDemiBold17;
            detailTextLabel.TextColor = A.Color_0B3239;
            detailTextLabel.Font = A.Font_AvenirNextRegular14;

            labelView.Hidden = hasImage;
            labelView.Text = ContactsHelper.NameToLetters (displayName);
            labelView.BackgroundColor = Util.ColorForUser (colorIndex);

            // Attendee Meeting Status
            var attendeeResponseImageView = cell.ViewWithTag (USER_RESPONSE_TAG) as UIImageView;
            if (null != GetImageForAttendeeResponse (attendee)) {
                attendeeResponseImageView.Image = GetImageForAttendeeResponse (attendee);
                cell.ViewWithTag (USER_RESPONSE_VIEW_TAG).Hidden = false;
            } else {
                cell.ViewWithTag (USER_RESPONSE_VIEW_TAG).Hidden = true;
            }

            //Separator line
            var separatorLine = view.ViewWithTag (SEPARATOR_LINE_TAG);
            var totalRow = tableView.NumberOfRowsInSection (indexPath.Section);
            if (totalRow - 1 == indexPath.Row) {
                separatorLine.Frame = new CGRect (0, 79.5f, cell.Frame.Width, .5f);
            } else {
                separatorLine.Frame = new CGRect (70 + xOffset, 79.5f, cell.Frame.Width - 60 - xOffset, .5f);
            }

        }

        protected void SetMultiSelectIcon (UIImageView iv, NSIndexPath indexPath)
        {
            if (multiSelect.ContainsKey (indexPath)) {
                iv.Image = UIImage.FromBundle ("gen-checkbox-checked");
                iv.UserInteractionEnabled = true;        
            } else {
                iv.Image = UIImage.FromBundle ("gen-checkbox");
                iv.UserInteractionEnabled = false;
            }
        }

        //        public override void RowSelected (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
        //        {
        //            UITableViewCell cell = tableView.CellAt (indexPath);
        //            if (isMultiSelecting) {
        //                var iv = cell.ViewWithTag (MULTI_ICON_TAG) as UIImageView;
        //                ToggleMultiSelectIcon (iv);
        //
        //                var attendee = AttendeeList [indexPath.Row];
        //                if (multiSelect.ContainsKey (indexPath)) {
        //                    multiSelect.Remove (indexPath);
        //                } else {
        //                    multiSelect.Add (indexPath, attendee);
        //                }
        //                vc.ConfigureNavBar (multiSelect.Count);
        //            }
        //            tableView.DeselectRow (indexPath, true);
        //        }

        protected void ToggleMultiSelectIcon (UIImageView iv)
        {
            if (iv.UserInteractionEnabled) {
                iv.Image = UIImage.FromBundle ("gen-checkbox");
                iv.UserInteractionEnabled = false;
            } else {
                iv.Image = UIImage.FromBundle ("gen-checkbox-checked");
                iv.UserInteractionEnabled = true;
            }
        }

        public void ResendInvite (McAttendee attendee)
        {
            UIAlertView alert = new UIAlertView ();
            alert.Title = "Resend invite?";
            alert.Message = attendee.Email;
            alert.AddButton ("Cancel");
            alert.AddButton ("Yes");
            alert.CancelButtonIndex = 0;
            alert.Dismissed += (object alertSender, UIButtonEventArgs alertEvent) => {
                if (1 == alertEvent.ButtonIndex) {
                    owner.SendAttendeeInvite (attendee); 
                }
            };
            alert.Show ();
        }

        protected void CallSwipeHandler (McAttendee attendee)
        {
            McContact contact = McContact.QueryByEmailAddress (Account.Id, attendee.Email).FirstOrDefault ();
            if (null == contact) {
                NcContactGleaner.GleanContacts (attendee.Email, Account.Id, false);
                contact = McContact.QueryByEmailAddress (Account.Id, attendee.Email).FirstOrDefault ();
            }
            owner.CallSwipeHandler (contact);
        }

        protected void EmailSwipeHandler (McAttendee attendee)
        {
            McContact contact = McContact.QueryByEmailAddress (Account.Id, attendee.Email).FirstOrDefault ();
            if (null == contact) {
                NcContactGleaner.GleanContacts (attendee.Email, Account.Id, false);
                contact = McContact.QueryByEmailAddress (Account.Id, attendee.Email).FirstOrDefault ();
            }
            owner.EmailSwipeHandler (contact);
        }

        public void ChangeAttendeeType (UITableViewCell cell, McAttendee attendee)
        {
            if (NcAttendeeType.Required == attendee.AttendeeType) {
                attendee.AttendeeType = NcAttendeeType.Optional;
            } else {
                attendee.AttendeeType = NcAttendeeType.Required;
            }
            owner.UpdateLists ();
            owner.ConfigureAttendeeTable ();
            if (!editing) {
                owner.SyncRequest ();
            }
            return;
        }

        public void RemoveAttendee (McAttendee attendee)
        {
            owner.RemoveAttendee (attendee);
        }

        public UIImage GetImageForAttendeeResponse (McAttendee attendee)
        {
            var reponseImage = new UIImage ();
            if (attendee.AttendeeStatus == NcAttendeeStatus.Accept) {
                reponseImage = UIImage.FromBundle ("btn-mtng-accept-pressed");
                return reponseImage;
            }
            if (attendee.AttendeeStatus == NcAttendeeStatus.Tentative) {
                reponseImage = UIImage.FromBundle ("btn-mtng-tenative-pressed");
                return reponseImage;
            }
            if (attendee.AttendeeStatus == NcAttendeeStatus.Decline) {
                reponseImage = UIImage.FromBundle ("btn-mtng-decline-pressed");
                return reponseImage;
            }
            return null;
        }

        public void MakeEmptyCircle (UIView parentView)
        {
            var attendeeResponseView = new UIView (new CGRect (2.5f, 2.5f, 15, 15));
            attendeeResponseView.BackgroundColor = UIColor.White;
            attendeeResponseView.Layer.CornerRadius = 15 / 2;
            attendeeResponseView.Layer.BorderColor = A.Color_NachoLightGrayBackground.CGColor;
            attendeeResponseView.Layer.BorderWidth = 1;
            parentView.Add (attendeeResponseView);
        }
    }
}

