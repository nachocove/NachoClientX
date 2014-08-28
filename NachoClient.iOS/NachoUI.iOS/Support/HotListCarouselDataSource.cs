﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using iCarouselBinding;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MCSwipeTableViewCellBinding;
using MonoTouch.Dialog;
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public class HotListCarouselDataSource : iCarouselDataSource
    {
        protected const int USER_IMAGE_TAG = 101;
        protected const int USER_LABEL_TAG = 109;
        protected const int FROM_TAG = 102;
        protected const int SUBJECT_TAG = 103;
        protected const int PREVIEW_TAG = 104;
        protected const int REMINDER_ICON_TAG = 105;
        protected const int REMINDER_TEXT_TAG = 106;
        protected const int ATTACHMENT_TAG = 107;
        protected const int RECEIVED_DATE_TAG = 108;
        static List<UIView> PreventViewGC;
        static List<UIBarButtonItem> preventBarButtonGC;
        NachoNowViewController owner;

        public HotListCarouselDataSource (NachoNowViewController o)
        {
            owner = o;
        }

        public override uint NumberOfItemsInCarousel (iCarousel carousel)
        {
            if (null != owner) {
                if (null != owner.priorityInbox) {
                    return (uint)owner.priorityInbox.Count ();
                }
            }
            return 0;
        }

        public override UIView ViewForItemAtIndex (iCarousel carousel, uint index, UIView view)
        {
            // Create new view if no view is available for recycling
            if (view == null) {
                view = CreateView (carousel);
                if (null == PreventViewGC) {
                    PreventViewGC = new List<UIView> ();
                }
                PreventViewGC.Add (view);
            }
            ConfigureView (view, (int)index);
            return view;
        }

        /// <summary>
        /// Create the views, not the values, of the cell.
        /// </summary>
        protected UIView CreateView (iCarousel carousel)
        {
            var carouselFrame = carousel.Frame;
            var frame = new RectangleF (0, 0, carouselFrame.Width - 30.0f, carouselFrame.Height - 0.0f);
            var view = new UIView (frame);
            view.BackgroundColor = UIColor.White;
            view.AutoresizingMask = UIViewAutoresizing.None;
            view.ContentMode = UIViewContentMode.Center;
            view.Layer.CornerRadius = 5;
            view.Layer.MasksToBounds = true;
            view.Layer.BorderColor = A.Color_NachoNowBackground.CGColor;
            view.Layer.BorderWidth = 1;

            var viewWidth = view.Frame.Width;

            // User image view
            var userImageView = new UIImageView (new RectangleF (15, 15, 40, 40));
            userImageView.Layer.CornerRadius = 20;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Tag = USER_IMAGE_TAG;
            view.AddSubview (userImageView);

            // User userLabelView view, if no image
            var userLabelView = new UILabel (new RectangleF (15, 15, 40, 40));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 20;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Tag = USER_LABEL_TAG;
            view.AddSubview (userLabelView);

            // From label view
            // Font will vary bold or regular, depending on isRead.
            // Size fields will be recalculated after text is known.
            var fromLabelView = new UILabel (new RectangleF (65, 20, 150, 20));
            fromLabelView.Font = A.Font_AvenirNextDemiBold17;
            fromLabelView.TextColor = A.Color_0F424C;
            fromLabelView.Tag = FROM_TAG;
            view.AddSubview (fromLabelView);

            // Subject label view
            // Size fields will be recalculated after text is known.
            // TODO: Confirm 'y' of Subject
            var subjectLabelView = new UILabel (new RectangleF (65, 40, viewWidth - 15 - 65, 20));
            subjectLabelView.LineBreakMode = UILineBreakMode.TailTruncation;
            subjectLabelView.Font = A.Font_AvenirNextMedium14;
            subjectLabelView.TextColor = A.Color_0F424C;
            subjectLabelView.Tag = SUBJECT_TAG;
            view.AddSubview (subjectLabelView);

            // Preview label view
            // Size fields will be recalculated after text is known
            var previewLabelView = new UILabel (new RectangleF (12, 60, viewWidth - 15 - 12, 120));
            previewLabelView.Font = A.Font_AvenirNextRegular14;
            previewLabelView.TextColor = A.Color_999999;
            previewLabelView.Lines = 0;
            previewLabelView.Tag = PREVIEW_TAG;
            view.AddSubview (previewLabelView);

            //                // Reminder image view
            //                var reminderImageView = new UIImageView (new RectangleF (65, 119, 12, 12));
            //                reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
            //                reminderImageView.Tag = REMINDER_ICON_TAG;
            //                view.AddSubview (reminderImageView);
            //
            //                // Reminder label view
            //                var reminderLabelView = new UILabel (new RectangleF (87, 115, 230, 20));
            //                reminderLabelView.Font = A.Font_AvenirNextRegular14;
            //                reminderLabelView.TextColor = A.Color_9B9B9B;
            //                reminderLabelView.Tag = REMINDER_TEXT_TAG;
            //                view.AddSubview (reminderLabelView);

            // Attachment image view
            // Attachment 'x' will be adjusted to be left of date received field
            var attachmentImageView = new UIImageView (new RectangleF (200, 18, 16, 16));
            attachmentImageView.Image = UIImage.FromBundle ("inbox-icn-attachment");
            attachmentImageView.Tag = ATTACHMENT_TAG;
            view.AddSubview (attachmentImageView);

            // Received label view
            var receivedLabelView = new UILabel (new RectangleF (220, 18, 100, 20));
            receivedLabelView.Font = A.Font_AvenirNextRegular14;
            receivedLabelView.TextColor = A.Color_9B9B9B;
            receivedLabelView.TextAlignment = UITextAlignment.Right;
            receivedLabelView.Tag = RECEIVED_DATE_TAG;
            view.AddSubview (receivedLabelView);

            if (null == preventBarButtonGC) {
                preventBarButtonGC = new List<UIBarButtonItem> ();
            }

            var replyButton = new UIBarButtonItem (UIImage.FromBundle ("toolbar-icn-reply"), UIBarButtonItemStyle.Plain, null);
            replyButton.Clicked += (object sender, EventArgs e) => {
                ReplyActionSheet (view);
            };
            preventBarButtonGC.Add (replyButton);

            var chiliButton = new UIBarButtonItem (UIImage.FromBundle ("icn-nothot"), UIBarButtonItemStyle.Plain, null);
            chiliButton.Clicked += (object sender, EventArgs e) => {
                onChiliButtonClicked (view);
            };
            preventBarButtonGC.Add (chiliButton);

            var deferButton = new UIBarButtonItem (UIImage.FromBundle ("navbar-icn-defer"), UIBarButtonItemStyle.Plain, null);
            deferButton.Clicked += (object sender, EventArgs e) => {
                onDeferButtonClicked (view);
            };
            preventBarButtonGC.Add (deferButton);

            var saveButton = new UIBarButtonItem (UIImage.FromBundle ("toolbar-icn-move"), UIBarButtonItemStyle.Plain, null);
            saveButton.Clicked += (object sender, EventArgs e) => {
                onSaveButtonClicked (view);
            };
            preventBarButtonGC.Add (saveButton);

            var archiveButton = new UIBarButtonItem (UIImage.FromBundle ("icn-archive"), UIBarButtonItemStyle.Plain, null);
            archiveButton.Clicked += (object sender, EventArgs e) => {
                onArchiveButtonClicked (view);
            };
            preventBarButtonGC.Add (saveButton);

            var flexibleSpace = new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace);
            preventBarButtonGC.Add (flexibleSpace);

            var fixedSpace = new UIBarButtonItem (UIBarButtonSystemItem.FixedSpace);
            fixedSpace.Width = 25;
            preventBarButtonGC.Add (fixedSpace);

            var deleteButton = new UIBarButtonItem (UIImage.FromBundle ("toolbar-icn-delete"), UIBarButtonItemStyle.Plain, null);
            deleteButton.Clicked += (object sender, EventArgs e) => {
                onDeleteButtonClicked (view);
            };
            preventBarButtonGC.Add (deleteButton);

            var toolbar = new UIToolbar (new RectangleF (0, frame.Height - 44, frame.Width, 44));
            toolbar.SetItems (new UIBarButtonItem[] {
                replyButton,
                flexibleSpace,
                chiliButton,
                flexibleSpace,
                deferButton,
                flexibleSpace,
                archiveButton,
                flexibleSpace,
                saveButton,
                flexibleSpace,
                deleteButton
            }, false);
            view.AddSubview (toolbar);

            return view;
        }

        void onReplyButtonClicked (UIView view, string action)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            owner.PerformSegueForDelegate ("NachoNowToCompose", new SegueHolder (action, messageThread));
        }

        void onChiliButtonClicked (UIView view)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            var message = messageThread.SingleMessageSpecialCase ();
            message.ToggleHotOrNot ();
            owner.priorityInbox.Refresh ();
            owner.ReloadHotListData ();
        }

        void onDeferButtonClicked (UIView view)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            owner.PerformSegueForDelegate ("NachoNowToMessagePriority", new SegueHolder (messageThread));
        }

        void onSaveButtonClicked (UIView view)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            owner.PerformSegueForDelegate ("NachoNowToMessageAction", new SegueHolder (messageThread));
        }

        void onArchiveButtonClicked (UIView view)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            var message = messageThread.SingleMessageSpecialCase ();
            NcEmailArchiver.Archive (message);
        }

        void onDeleteButtonClicked (UIView view)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            var message = messageThread.SingleMessageSpecialCase ();
            NcEmailArchiver.Delete (message);
        }

        /// <summary>
        /// Populate message cells with data, adjust sizes and visibility
        /// </summary>
        protected void ConfigureView (UIView view, int messageThreadIndex)
        {
            // Save thread index

            view.Tag = messageThreadIndex;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            var message = messageThread.SingleMessageSpecialCase ();

            var viewWidth = view.Frame.Width;

            // User image view
            var userImageView = view.ViewWithTag (USER_IMAGE_TAG) as UIImageView;
            var userLabelView = view.ViewWithTag (USER_LABEL_TAG) as UILabel;
            userImageView.Hidden = true;
            userLabelView.Hidden = true;

            var userImage = Util.ImageOfSender (message.AccountId, Pretty.EmailString (message.From));

            if (null != userImage) {
                userImageView.Hidden = false;
                userImageView.Image = userImage;
            } else {
                userLabelView.Hidden = false;
                if (String.IsNullOrEmpty (message.cachedFromLetters) || (2 <= message.cachedFromColor)) {
                    Util.CacheUserMessageFields (message);
                }
                userLabelView.Text = message.cachedFromLetters;
                userLabelView.BackgroundColor = Util.ColorForUser (message.cachedFromColor);
            }

            // Subject label view
            var subjectLabelView = view.ViewWithTag (SUBJECT_TAG) as UILabel;
            subjectLabelView.Text = Pretty.SubjectString (message.Subject);

            // Preview label view
            var previewLabelView = view.ViewWithTag (PREVIEW_TAG) as UILabel;
            var rawPreview = message.GetBodyPreviewOrEmpty ();
            //                var cookedPreview = System.Text.RegularExpressions.Regex.Replace (rawPreview, @"\s+", " ");
            int oldLength;
            var cookedPreview = rawPreview;
            do {
                oldLength = cookedPreview.Length;
                cookedPreview = cookedPreview.Replace ('\r', '\n');
                cookedPreview = cookedPreview.Replace ("\n\n", "\n");
            } while(cookedPreview.Length != oldLength);
            previewLabelView.AttributedText = new NSAttributedString (cookedPreview);

            //                // Reminder image view and label
            //                var reminderImageView = cell.ViewWithTag (REMINDER_ICON_TAG) as UIImageView;
            //                var reminderLabelView = cell.ViewWithTag (REMINDER_TEXT_TAG) as UILabel;
            //                if (message.HasDueDate ()) {
            //                    reminderImageView.Hidden = false;
            //                    reminderLabelView.Hidden = false;
            //                    if (message.IsOverdue ()) {
            //                        reminderLabelView.Text = String.Format ("Response was due {0}", message.FlagDueAsUtc ());
            //                    } else {
            //                        reminderLabelView.Text = String.Format ("Response is due {0}", message.FlagDueAsUtc ());
            //                    }
            //                } else {
            //                    reminderImageView.Hidden = true;
            //                    reminderLabelView.Hidden = true;
            //                }

            // Received label view
            var receivedLabelView = view.ViewWithTag (RECEIVED_DATE_TAG) as UILabel;
            receivedLabelView.Text = Pretty.CompactDateString (message.DateReceived);
            receivedLabelView.SizeToFit ();
            var receivedLabelRect = receivedLabelView.Frame;
            receivedLabelRect.X = viewWidth - 15 - receivedLabelRect.Width;
            receivedLabelRect.Height = 20;
            receivedLabelView.Frame = receivedLabelRect;

            // Attachment image view
            var attachmentImageView = view.ViewWithTag (ATTACHMENT_TAG) as UIImageView;
            attachmentImageView.Hidden = false;
            var attachmentImageRect = attachmentImageView.Frame;
            attachmentImageRect.X = receivedLabelRect.X - 10 - 16;
            attachmentImageView.Frame = attachmentImageRect;

            // From label view
            var fromLabelView = view.ViewWithTag (FROM_TAG) as UILabel;
            var fromLabelRect = fromLabelView.Frame;
            fromLabelRect.Width = attachmentImageRect.X - 65;
            fromLabelView.Frame = fromLabelRect;
            fromLabelView.Text = Pretty.SenderString (message.From);
            fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextDemiBold17 : A.Font_AvenirNextRegular17);
        }

        public override uint NumberOfPlaceholdersInCarousel (iCarousel carousel)
        {
            if (0 == NumberOfItemsInCarousel (carousel)) {
                return 1;
            } else {
                return 0;
            }
        }

        public override UIView PlaceholderViewAtIndex (iCarousel carousel, uint index, UIView view)
        {
            //create new view if no view is available for recycling
            if (null == view) {
                var f = carousel.Frame;
                var frame = new RectangleF (f.X, f.Y, f.Width - 30.0f, f.Height - 30.0f);
                var v = new UIView (frame);
                v.ContentMode = UIViewContentMode.Center;
                v.BackgroundColor = UIColor.Blue;
                v.Layer.CornerRadius = 5;
                v.Layer.MasksToBounds = true;
                v.Layer.BorderColor = UIColor.DarkGray.CGColor;
                v.Layer.BorderWidth = 1;
                var l = new UILabel (v.Bounds);
                l.BackgroundColor = UIColor.White;
                l.TextAlignment = UITextAlignment.Center;
                l.Font = l.Font.WithSize (20f);
                l.Tag = 1;
                v.AddSubview (l);
                view = v;
            }
            var label = (UILabel)view.ViewWithTag (1);
            label.Text = "No hot items!";

            return view;
        }

        protected void ReplyActionSheet (UIView view)
        {
            var actionSheet = new UIActionSheet ();
            actionSheet.Add ("Reply");
            actionSheet.Add ("Reply All");
            actionSheet.Add ("Forward");
            actionSheet.Add ("Cancel");

            actionSheet.CancelButtonIndex = 3;

            actionSheet.Clicked += delegate(object a, UIButtonEventArgs b) {
                switch (b.ButtonIndex) {
                case 0:
                    onReplyButtonClicked (view, MessageComposeViewController.Reply);
                    break;
                case 1:
                    onReplyButtonClicked (view, MessageComposeViewController.ReplyAll);
                    break;
                case 2:
                    onReplyButtonClicked (view, MessageComposeViewController.Forward);
                    break;
                case 3:
                    break; // Cancel
                }
            };
            actionSheet.ShowInView (view);
        }
    }

    public class HotListCarouselDelegate : iCarouselDelegate
    {
        NachoNowViewController owner;

        public HotListCarouselDelegate (NachoNowViewController o)
        {
            owner = o;
        }

        public override void DidSelectItemAtIndex (iCarousel carousel, int index)
        {
            // Ignore placeholders
            if ((0 > index) || (owner.priorityInbox.Count () <= index)) {
                return;
            }
            var messageThread = owner.priorityInbox.GetEmailThread (index);
            owner.PerformSegue ("NachoNowToMessageView", new SegueHolder (messageThread));
        }

        /// <summary>
        /// Values for option.
        /// </summary>
        public override float ValueForOption (iCarousel carousel, iCarouselOption option, float value)
        {
            // customize carousel display
            switch (option) {
            case iCarouselOption.Wrap:
                // normally you would hard-code this to true or false
                return (owner.wrap ? 1.0f : 0.0f);
            case iCarouselOption.Spacing:
                // add a bit of spacing between the item views
                return value * 1.02f;
            case iCarouselOption.FadeMax:
                if (iCarouselType.Custom == carousel.Type) {
                    return 0.0f;
                }
                return value;
            default:
                return value;
            }

        }

        public override void CarouselWillBeginDragging (iCarousel carousel)
        {
            Log.Info (Log.LOG_UI, "DraggingStarted");
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStarted),
                Account = ConstMcAccount.NotAccountSpecific,
            });
        }

        public override void CarouselDidEndDragging (iCarousel carousel, bool decelerate)
        {
            Log.Info (Log.LOG_UI, "DraggingEnded");
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStopped),
                Account = ConstMcAccount.NotAccountSpecific,
            });
        }
    }
}

