//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using iCarouselBinding;
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
        protected const int FROM_TAG = 102;
        protected const int SUBJECT_TAG = 103;
        public const int PREVIEW_TAG = 104;
        protected const int REMINDER_ICON_TAG = 105;
        protected const int REMINDER_TEXT_TAG = 106;
        protected const int ATTACHMENT_TAG = 107;
        protected const int RECEIVED_DATE_TAG = 108;
        protected const int USER_LABEL_TAG = 109;
        protected const int USER_CHILI_TAG = 110;
        static List<UIView> PreventViewGC;
        static List<UIBarButtonItem> preventBarButtonGC;
        NachoNowViewController owner;

        // Pre-made swipe action descriptors
        private static SwipeActionDescriptor ARCHIVE_BUTTON =
            new SwipeActionDescriptor (0.25f, UIImage.FromBundle ("email-archive-gray"),
                "Archive", A.Color_NachoSwipeActionRed);
        private static SwipeActionDescriptor SAVE_BUTTON =
            new SwipeActionDescriptor (0.25f, UIImage.FromBundle ("email-putintofolder-gray"),
                "Save", A.Color_NachoSwipeActionGreen);
        private static SwipeActionDescriptor DELETE_BUTTON =
            new SwipeActionDescriptor (0.25f, UIImage.FromBundle ("email-delete-gray"),
                "Delete", A.Color_NachoSwipeActionBlue);
        private static SwipeActionDescriptor DEFER_BUTTON =
            new SwipeActionDescriptor (0.25f, UIImage.FromBundle ("email-defer-gray"),
                "Defer", A.Color_NachoSwipeActionYellow);

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
            var frame = new RectangleF (0, 10, carouselFrame.Width - 15.0f, carouselFrame.Height - 30);
            var view = new SwipeActionView (frame);
            view.SetAction (ARCHIVE_BUTTON, SwipeSide.RIGHT);
            view.SetAction (DELETE_BUTTON, SwipeSide.RIGHT);
            view.SetAction (SAVE_BUTTON, SwipeSide.LEFT);
            view.SetAction (DEFER_BUTTON, SwipeSide.LEFT);
            view.OnClick = (SwipeSide side, int index) => {
                if (SwipeSide.LEFT == side) {
                    switch (index) {
                    case 0:
                        onSaveButtonClicked (view);
                        break;
                    case 1:
                        onDeferButtonClicked (view);
                        break;
                    default:
                        var message = String.Format ("Unknown left index {0}", index);
                        throw new NcAssert.NachoDefaultCaseFailure (message);
                    }
                } else if (SwipeSide.RIGHT == side) {
                    switch (index) {
                    case 0:
                        onArchiveButtonClicked (view);
                        break;
                    case 1:
                        onDeleteButtonClicked (view);
                        break;
                    default:
                        var message = String.Format ("Unknwon right index {0}", index);
                        throw new NcAssert.NachoDefaultCaseFailure(message);
                    }
                } else {
                    NcAssert.True (false);
                }
            };
            view.OnSwipe = (SwipeActionView.SwipeState state) => {
                switch (state) {
                case SwipeActionView.SwipeState.SWIPE_BEGIN:
                    owner.carouselView.ScrollEnabled = false;
                    owner.Selectable = false;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    owner.carouselView.ScrollEnabled = true;
                    owner.Selectable = true;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    owner.carouselView.ScrollEnabled = true;
                    owner.Selectable = false;
                    break;
                default:
                    var message = String.Format ("Unknown swipe state {0}", (int)state);
                    throw new NcAssert.NachoDefaultCaseFailure (message);
                }
            };
            view.BackgroundColor = UIColor.White;
            view.AutoresizingMask = UIViewAutoresizing.None;
            view.ContentMode = UIViewContentMode.Center;
            view.Layer.CornerRadius = 5;
            view.Layer.MasksToBounds = true;
            view.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            view.Layer.BorderWidth = .5f;

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
            var fromLabelView = new UILabel (new RectangleF (65, 10, 150, 20));
            fromLabelView.Font = A.Font_AvenirNextDemiBold17;
            fromLabelView.TextColor = A.Color_0F424C;
            fromLabelView.Tag = FROM_TAG;
            view.AddSubview (fromLabelView);

            // Subject label view
            // Size fields will be recalculated after text is known.
            // TODO: Confirm 'y' of Subject
            var subjectLabelView = new UILabel (new RectangleF (65, 30, viewWidth - 15 - 65, 20));
            subjectLabelView.LineBreakMode = UILineBreakMode.TailTruncation;
            subjectLabelView.Font = A.Font_AvenirNextMedium14;
            subjectLabelView.TextColor = A.Color_0F424C;
            subjectLabelView.Tag = SUBJECT_TAG;
            view.AddSubview (subjectLabelView);

            // Received label view
            var receivedLabelView = new UILabel (new RectangleF (64, 50, 250, 20));
            receivedLabelView.Font = A.Font_AvenirNextRegular14;
            receivedLabelView.TextColor = A.Color_9B9B9B;
            receivedLabelView.Tag = RECEIVED_DATE_TAG;
            view.AddSubview (receivedLabelView);

            var bottomY = frame.Height - 44; // toolbar height is 44

            // Reminder image view
            var reminderImageView = new UIImageView (new RectangleF (12, 70 + 4, 12, 12));
            reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
            reminderImageView.Tag = REMINDER_ICON_TAG;
            view.AddSubview (reminderImageView);

            // Reminder label view
            var reminderLabelView = new UILabel (new RectangleF (34, 70, 230, 20));
            reminderLabelView.Font = A.Font_AvenirNextRegular14;
            reminderLabelView.TextColor = A.Color_9B9B9B;
            reminderLabelView.Tag = REMINDER_TEXT_TAG;
            view.AddSubview (reminderLabelView);

            // Preview label view
            // Size fields will be recalculated after text is known
            var previewLabelView = new BodyView (new RectangleF (12, 70, viewWidth - 15 - 12, bottomY - 60), view);
            previewLabelView.Tag = PREVIEW_TAG;
            previewLabelView.UserInteractionEnabled = false;
            view.AddSubview (previewLabelView);

            // Chili image view
            float rightMargin = viewWidth - 15;
            float chiliX = rightMargin - 20;
            var chiliImageView = new UIImageView (new RectangleF (chiliX, 8, 20, 20));
            chiliImageView.Image = UIImage.FromBundle ("icn-red-chili-small");
            chiliImageView.Tag = USER_CHILI_TAG;
            view.AddSubview (chiliImageView);

            // Attachment image view
            // Attachment 'x' will be adjusted to be left of chili field
            var attachmentImageView = new UIImageView (new RectangleF (chiliX - 10 - 16, 52, 16, 16));
            attachmentImageView.Image = UIImage.FromBundle ("inbox-icn-attachment");
            attachmentImageView.Tag = ATTACHMENT_TAG;
            view.AddSubview (attachmentImageView);

            if (null == preventBarButtonGC) {
                preventBarButtonGC = new List<UIBarButtonItem> ();
            }
                
            var toolbar = new MessageToolbar (new RectangleF (0, frame.Height - 44, frame.Width, 44));
            toolbar.OnClick = (object sender, EventArgs e) => {
                var toolbarEventArgs = e as MessageToolbarEventArgs;
                switch (toolbarEventArgs.Action) {
                case MessageToolbar.ActionType.REPLY:
                    onReplyButtonClicked (view, MessageComposeViewController.REPLY_ACTION);
                    break;
                case MessageToolbar.ActionType.REPLY_ALL:
                    onReplyButtonClicked (view, MessageComposeViewController.REPLY_ALL_ACTION);
                    break;
                case MessageToolbar.ActionType.FORWARD:
                    onReplyButtonClicked (view, MessageComposeViewController.FORWARD_ACTION);
                    break;
                case MessageToolbar.ActionType.ARCHIVE:
                    onArchiveButtonClicked (view);
                    break;
                case MessageToolbar.ActionType.DELETE:
                    onDeleteButtonClicked (view);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("unknown toolbar action {0}",
                        (int)toolbarEventArgs.Action));
                }
            };
            view.AddSubview (toolbar);

            return view;
        }

        void onReplyButtonClicked (UIView view, string action)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            if (null == messageThread) {
                return;
            }
            owner.PerformSegueForDelegate ("NachoNowToCompose", new SegueHolder (action, messageThread));
        }

        void onChiliButtonClicked (UIView view)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            if (null == messageThread) {
                return;
            }
            var message = messageThread.SingleMessageSpecialCase ();
            message.ToggleHotOrNot ();
            owner.priorityInbox.Refresh ();
            owner.ReloadHotListData ();
        }

        void onDeferButtonClicked (UIView view)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            if (null == messageThread) {
                return;
            }
            owner.PerformSegueForDelegate ("NachoNowToMessagePriority", new SegueHolder (messageThread));
        }

        void onSaveButtonClicked (UIView view)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            if (null == messageThread) {
                return;
            }
            owner.PerformSegueForDelegate ("NachoNowToFolders", new SegueHolder (messageThread));
        }

        void onArchiveButtonClicked (UIView view)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            if (null == messageThread) {
                return;
            }
            var message = messageThread.SingleMessageSpecialCase ();
            NcEmailArchiver.Archive (message);
        }

        void onDeleteButtonClicked (UIView view)
        {
            var messageThreadIndex = view.Tag;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);
            if (null == messageThread) {
                return;
            }
            var message = messageThread.SingleMessageSpecialCase ();
            NcEmailArchiver.Delete (message);
        }

        private string GetPreview (McEmailMessage message)
        {
            string error;
            string preview = message.GetBodyPreviewOrEmpty ();
            if (String.Empty != preview) {
                return preview;
            }
            preview = MimeHelpers.ExtractTextPartWithError (message, out error);
            if (String.IsNullOrEmpty (preview)) {
                preview = " ";
            }

            // Truncate the body to 1000 characters
            if (preview.Length > 1000) {
                preview = preview.Substring (0, 1000);
            }

            // Cache a truncated version of preview
            message.BodyPreview = preview;
            message.Update ();

            return preview;
        }

        protected void HandleUnavailableMessage (UIView view)
        {
            foreach (var s in view.Subviews) {
                s.Hidden = true;
            }
            var slv = view.ViewWithTag (SUBJECT_TAG) as UILabel;
            slv.Text = "This message is unavailable";
            slv.Hidden = false;
        }

        /// <summary>
        /// Populate message cells with data, adjust sizes and visibility
        /// </summary>
        protected void ConfigureView (UIView view, int messageThreadIndex)
        {           
            // Save thread index
            view.Tag = messageThreadIndex;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);

            if (null == messageThread) {
                HandleUnavailableMessage (view);
                return;
            }

            var message = messageThread.SingleMessageSpecialCase ();
            if (null == message) {
                HandleUnavailableMessage (view);
                return;
            }


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
            if (String.IsNullOrEmpty (message.Subject)) {
                subjectLabelView.TextColor = A.Color_9B9B9B;
            }
            subjectLabelView.Hidden = false;

            float previewLabelAdjustment = 0;

            // Reminder image view and label
            var reminderImageView = view.ViewWithTag (REMINDER_ICON_TAG) as UIImageView;
            var reminderLabelView = view.ViewWithTag (REMINDER_TEXT_TAG) as UILabel;
            if (message.HasDueDate () || message.IsDeferred ()) {
                reminderImageView.Hidden = false;
                reminderLabelView.Hidden = false;
                reminderLabelView.Text = Pretty.ReminderText (message);
                previewLabelAdjustment = 24;
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
            }

            // Size of preview, depends on reminder view
            var previewLabelView = view.ViewWithTag (PREVIEW_TAG) as BodyView;
            previewLabelView.Hidden = false;

            var previewLabelViewHeight = view.Frame.Height - 80 - previewLabelAdjustment;
            previewLabelViewHeight -= 44; // toolbar
            previewLabelViewHeight -= 4; // padding

            previewLabelView.Configure (message);
            previewLabelView.Layout (previewLabelView.Frame.X, previewLabelView.Frame.Y,
                previewLabelView.Frame.Width, previewLabelViewHeight);

            // Received label view
            var receivedLabelView = view.ViewWithTag (RECEIVED_DATE_TAG) as UILabel;
            receivedLabelView.Text = Pretty.FullDateTimeString (message.DateReceived);
            receivedLabelView.SizeToFit ();
            receivedLabelView.Hidden = false;

            // Attachment image view
            var attachmentImageView = view.ViewWithTag (ATTACHMENT_TAG) as UIImageView;
            attachmentImageView.Hidden = !message.cachedHasAttachments;
            var attachmentImageRect = attachmentImageView.Frame;
            attachmentImageRect.X = receivedLabelView.Frame.Right + 10;;
            attachmentImageView.Frame = attachmentImageRect;

            // Chili image view - nothing to do. It is also shown
            var chiliImageView = view.ViewWithTag (USER_CHILI_TAG) as UIImageView;
            chiliImageView.Hidden = false;

            // From label view
            var fromLabelView = view.ViewWithTag (FROM_TAG) as UILabel;
            var fromLabelRect = fromLabelView.Frame;
            fromLabelRect.Width = attachmentImageRect.X - chiliImageView.Frame.Width - 10;
            fromLabelView.Frame = fromLabelRect;
            fromLabelView.Text = Pretty.SenderString (message.From);
            fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextDemiBold17 : A.Font_AvenirNextRegular17);
            fromLabelView.Hidden = false;
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
    }

    public class HotListCarouselDelegate : iCarouselDelegate
    {
        NachoNowViewController owner;

        public HotListCarouselDelegate (NachoNowViewController o)
        {
            owner = o;
        }

        public override bool ShouldSelectItemAtIndex (iCarousel carousel, int index)
        {
            return owner.Selectable;
        }

        public override void DidSelectItemAtIndex (iCarousel carousel, int index)
        {
            // Ignore placeholders
            if ((0 > index) || (owner.priorityInbox.Count () <= index)) {
                return;
            }
            var messageThread = owner.priorityInbox.GetEmailThread (index);
            if (null == messageThread) {
                return;
            }
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
            case iCarouselOption.VisibleItems:
                // We pre-render 16 items. The assumption is that one swipe cannot
                // change more than 16 items. When the user lifts the finger off
                // the screen and get ready for another swipe, it will have a couple
                // seconds to pre-render another set of items.
                return 16.0f;
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

