//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections.Generic;
using MCSwipeTableViewCellBinding;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using MonoTouch.CoreGraphics;

namespace NachoClient.iOS
{
    public class AttendeeTableViewSource : UITableViewSource
    {
        List<McAttendee> AttendeeList = new List<McAttendee> ();
        protected McAccount Account;
        protected bool editing = false;
        public IAttendeeTableViewSourceDelegate owner;

        protected const string UICellReuseIdentifier = "UICell";
        protected const string AttendeeCellReuseIdentifier = "AttendeeCell";

        public AttendeeTableViewSource ()
        {
            owner = null;
        }

        public void SetOwner (IAttendeeTableViewSourceDelegate owner)
        {
            this.owner = owner;
        }

        public void SetAttendeeList (List<McAttendee> attendees)
        {
            this.AttendeeList = new List<McAttendee> ();
            foreach (var attendee in attendees) {
                this.AttendeeList.Add (attendee);
            }
        }

        public void SetEditing (bool editing)
        {
            this.editing = editing;
        }

        public void SetAccount (McAccount account)
        {
            this.Account = account;
        }

        public List<McAttendee> GetAttendeeList ()
        {
            return this.AttendeeList;
        }

        /// <summary>
        /// Tableview delegate
        /// </summary>
        public override int NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        /// <summary>
        /// The number of rows in the specified section.
        /// </summary>
        public override int RowsInSection (UITableView tableview, int section)
        {
            return AttendeeList.Count;
        }

        //        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        //        {
        //            McContact contact;
        //
        //            contact = AttendeeList [indexPath.Row].GetContact ();
        //
        //            owner.ContactSelectedCallback (contact);
        //        }
        //
        //        public override void AccessoryButtonTapped (UITableView tableView, NSIndexPath indexPath)
        //        {
        //            McContact contact;
        //
        //            contact = AttendeeList [indexPath.Row].GetContact ();
        //
        //            owner.ContactSelectedCallback (contact);
        //        }

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
                labelView.Frame = new RectangleF (0, 0, label.Frame.Width + 50, label.Frame.Height);
            } else {
                labelView.Frame = new RectangleF (65, 0, label.Frame.Width + 50, label.Frame.Height);
            }
            labelView.Add (label);
            return labelView;
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return 69;
        }

        protected const int USER_NAME_TAG = 333;
        protected const int USER_LABEL_TAG = 334;
        protected const int USER_EMAIL_TAG = 335;
        protected const int USER_RESPONSE_TAG = 336;
        protected const int USER_IMAGE_TAG = 337;

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            McAttendee attendee;
            attendee = AttendeeList [indexPath.Row];

            var cell = CreateCell ();
            ConfigureCell (cell, attendee);

            return cell;
        }

        void ConfigureSwipes (MCSwipeTableViewCell cell, McAttendee attendee)
        {
            cell.FirstTrigger = 0.20f;
            cell.SecondTrigger = 0.50f;

            UIView resendView = null;
            UIColor blueColor = null;
            UIView crossView = null;
            UIColor redColor = null;
            UIView typeView = null;
            UIColor yellowColor = null;
            UIView deleteView = null;
            UIColor brownColor = null;

            try { 
                resendView = ViewWithLabel ("resend invite", "left");
                blueColor = new UIColor (A.Color_NachoBlue.CGColor);
                cell.SetSwipeGestureWithView (resendView, blueColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State1, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    ResendInvite (attendee);
                });
                crossView = ViewWithLabel ("resend invite", "left");
                cell.SetSwipeGestureWithView (crossView, blueColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State2, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    ResendInvite (attendee);
                });
                if (NcAttendeeType.Optional == attendee.AttendeeType) {
                    typeView = ViewWithLabel ("attendee required", "right");
                    yellowColor = new UIColor (A.Color_NachoGreen.CGColor);
                }
                if (NcAttendeeType.Required == attendee.AttendeeType) {
                    typeView = ViewWithLabel ("attendee optional", "right");
                    yellowColor = new UIColor (A.Color_NachoYellow.CGColor);
                }
                cell.SetSwipeGestureWithView (typeView, yellowColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State3, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    ChangeAttendeeType (cell, attendee);
                });
                deleteView = ViewWithLabel ("remove attendee", "right");
                redColor = new UIColor (A.Color_NachoRed.CGColor);
                cell.SetSwipeGestureWithView (deleteView, redColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State4, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    RemoveAttendee (attendee);
                });
            } finally {
                if (null != resendView) {
                    resendView.Dispose ();
                }
                if (null != blueColor) {
                    blueColor.Dispose ();
                }
                if (null != crossView) {
                    crossView.Dispose ();
                }
                if (null != redColor) {
                    redColor.Dispose ();
                }
                if (null != typeView) {
                    typeView.Dispose ();
                }
                if (null != yellowColor) {
                    yellowColor.Dispose ();
                }
                if (null != deleteView) {
                    deleteView.Dispose ();
                }
                if (null != brownColor) {
                    brownColor.Dispose ();
                }
            }
        }

        public MCSwipeTableViewCell CreateCell ()
        {
            var cell = new MCSwipeTableViewCell (UITableViewCellStyle.Subtitle, AttendeeCellReuseIdentifier);
            NcAssert.True (null != cell);

            if (null == cell.ViewWithTag (USER_NAME_TAG)) {
                var userNameView = new UILabel (new RectangleF (70, 22 - 2.5f, 320 - 15 - 65, 20));
                userNameView.LineBreakMode = UILineBreakMode.TailTruncation;
                userNameView.Tag = USER_NAME_TAG;
                cell.ContentView.AddSubview (userNameView);
                var userEmailView = new UILabel (new RectangleF (70, 40 - 2.5f, 320 - 15 - 65, 20));
                userEmailView.LineBreakMode = UILineBreakMode.TailTruncation;
                userEmailView.Tag = USER_EMAIL_TAG;
                cell.ContentView.AddSubview (userEmailView);

                // User image
                var userImageView = new UIImageView (new RectangleF (15, 15 - 2.5f, 45, 45));
                userImageView.Layer.CornerRadius = (45.0f / 2.0f);
                userImageView.Layer.MasksToBounds = true;
                userImageView.Tag = USER_IMAGE_TAG;
                cell.ContentView.AddSubview (userImageView);

                // User userLabelView view, if no image
                var userLabelView = new UILabel (new RectangleF (15, 15 - 2.5f, 45, 45));
                userLabelView.Font = A.Font_AvenirNextRegular24;
                userLabelView.TextColor = UIColor.White;
                userLabelView.TextAlignment = UITextAlignment.Center;
                userLabelView.LineBreakMode = UILineBreakMode.Clip;
                userLabelView.Layer.CornerRadius = (45 / 2);
                userLabelView.Layer.MasksToBounds = true;
                userLabelView.Tag = USER_LABEL_TAG;
                cell.ContentView.AddSubview (userLabelView);

                var attendeeResponseView = new UIView (new RectangleF (15 + 27, 42 - 2.5f, 20, 20));
                attendeeResponseView.BackgroundColor = UIColor.White;
                attendeeResponseView.Layer.CornerRadius = 10;
                MakeEmptyCircle (attendeeResponseView);
                UIImageView responseImageView = new UIImageView (new RectangleF (2.5f, 2.5f, 15, 15));
                responseImageView.Tag = USER_RESPONSE_TAG;
                attendeeResponseView.Add (responseImageView);

                cell.ContentView.AddSubview (attendeeResponseView);
            }
            return cell;
        }

        public void ConfigureCell (MCSwipeTableViewCell cell, McAttendee attendee)
        {
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
                
            cell.TextLabel.Text = null;
            cell.DetailTextLabel.Text = null;

            ConfigureSwipes (cell, attendee);

            var TextLabel = cell.ViewWithTag (USER_NAME_TAG) as UILabel;
            var DetailTextLabel = cell.ViewWithTag (USER_EMAIL_TAG) as UILabel;
            var labelView = cell.ViewWithTag (USER_LABEL_TAG) as UILabel;
            var imageView = cell.ViewWithTag (USER_IMAGE_TAG) as UIImageView;
            bool hasImage = false;

            using (UIImage image = Util.ImageOfSender ((int)owner.GetAccountId (), attendee.Email)) {
                if (null != image) {
                    imageView.Image = image;
                    hasImage = true;
                }
            }

            // Both empty
            if (String.IsNullOrEmpty (displayName) && String.IsNullOrEmpty (displayEmailAddress)) {
                TextLabel.Text = "Contact has no name or email address";
                TextLabel.TextColor = UIColor.LightGray;
                TextLabel.Font = A.Font_AvenirNextRegular14;
                labelView.Hidden = true;
                return;
            }

            // Name empty
            if (String.IsNullOrEmpty (displayName)) {
                TextLabel.Text = displayEmailAddress;
                TextLabel.TextColor = A.Color_0B3239;
                TextLabel.Font = A.Font_AvenirNextDemiBold17;
                labelView.Hidden = hasImage;
                labelView.Text = Util.NameToLetters (displayEmailAddress);
                labelView.BackgroundColor = Util.ColorForUser (colorIndex);
                return;
            }

            // Email empty
            if (String.IsNullOrEmpty (displayEmailAddress)) {
                TextLabel.Text = displayName;
                DetailTextLabel.Text = "Contact has no email address";
                TextLabel.TextColor = A.Color_0B3239;
                TextLabel.Font = A.Font_AvenirNextDemiBold17;
                DetailTextLabel.TextColor = UIColor.LightGray;
                DetailTextLabel.Font = A.Font_AvenirNextRegular12;
                labelView.Hidden = hasImage;
                labelView.Text = Util.NameToLetters (displayName);
                labelView.BackgroundColor = Util.ColorForUser (colorIndex);
                return;
            }

            // Everything
            TextLabel.Text = displayName;
            DetailTextLabel.Text = displayEmailAddress;
            TextLabel.TextColor = A.Color_0B3239;
            TextLabel.Font = A.Font_AvenirNextDemiBold17;
            DetailTextLabel.TextColor = A.Color_0B3239;
            DetailTextLabel.Font = A.Font_AvenirNextRegular14;

            labelView.Hidden = hasImage;
            labelView.Text = Util.NameToLetters (displayName);
            labelView.BackgroundColor = Util.ColorForUser (colorIndex);

            // Attendee Meeting Status
            var attendeeResponseImageView = cell.ViewWithTag (USER_RESPONSE_TAG) as UIImageView;
            if (null != GetImageForAttendeeResponse (attendee)) {
                attendeeResponseImageView.Image = GetImageForAttendeeResponse (attendee);
            } else {
                attendeeResponseImageView.Hidden = true;
            }

            if (!editing) {
                cell.UserInteractionEnabled = false;
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

        public void ChangeAttendeeType (MCSwipeTableViewCell cell, McAttendee attendee)
        {
            if (NcAttendeeType.Optional == attendee.AttendeeType) {
                attendee.AttendeeType = NcAttendeeType.Required;
                owner.UpdateLists ();
                ConfigureCell (cell, attendee);
                owner.ConfigureAttendeeTable ();
                return;
            }
            if (NcAttendeeType.Required == attendee.AttendeeType) {
                attendee.AttendeeType = NcAttendeeType.Optional;
                owner.UpdateLists ();
                ConfigureCell (cell, attendee);
                owner.ConfigureAttendeeTable ();
                return;
            }
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
            var attendeeResponseView = new UIView (new RectangleF (2.5f, 2.5f, 15, 15));
            attendeeResponseView.BackgroundColor = UIColor.White;
            attendeeResponseView.Layer.CornerRadius = 15 / 2;
            attendeeResponseView.Layer.BorderColor = A.Color_NachoLightGrayBackground.CGColor;
            attendeeResponseView.Layer.BorderWidth = 1;
            parentView.Add (attendeeResponseView);
        }

        public override void DraggingStarted (UIScrollView scrollView)
        {
            NachoCore.Utils.NcAbate.HighPriority ("AttendeeTableViewSource DraggingStarted");
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            NachoCore.Utils.NcAbate.RegularPriority ("AttendeeTableViewSource DecelerationEnded");
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
                NachoCore.Utils.NcAbate.RegularPriority ("AttendeeTableViewSource Draggingended");
            }
        }
    }
}

