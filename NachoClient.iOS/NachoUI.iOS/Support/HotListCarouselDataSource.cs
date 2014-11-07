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
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public class HotListCarouselDataSource : iCarouselDataSource
    {
        public const int PLACEHOLDER_TAG = -1;
        protected const int USER_IMAGE_TAG = -101;
        protected const int USER_LABEL_TAG = -102;
        protected const int MESSAGE_HEADER_TAG = -103;
        public const int PREVIEW_TAG = -104;
        protected const int REMINDER_ICON_TAG = -105;
        protected const int REMINDER_TEXT_TAG = -106;
        protected const int USER_MORE_TAG = -107;
        protected const int UNAVAILABLE_TAG = -108;
        NachoNowViewController owner;

        private const int ARCHIVE_TAG = 1;
        private const int SAVE_TAG = 2;
        private const int DELETE_TAG = 3;
        private const int DEFER_TAG = 4;

        // Pre-made swipe action descriptors
        private static SwipeActionDescriptor ARCHIVE_BUTTON =
            new SwipeActionDescriptor (ARCHIVE_TAG, 0.25f, UIImage.FromBundle ("email-archive-gray"),
                "Archive", A.Color_NachoSwipeActionGreen);
        private static SwipeActionDescriptor SAVE_BUTTON =
            new SwipeActionDescriptor (SAVE_TAG, 0.25f, UIImage.FromBundle ("email-putintofolder-gray"),
                "Save", A.Color_NachoSwipeActionBlue);
        private static SwipeActionDescriptor DELETE_BUTTON =
            new SwipeActionDescriptor (DELETE_TAG, 0.25f, UIImage.FromBundle ("email-delete-gray"),
                "Delete", A.Color_NachoSwipeActionRed);
        private static SwipeActionDescriptor DEFER_BUTTON =
            new SwipeActionDescriptor (DEFER_TAG, 0.25f, UIImage.FromBundle ("email-defer-gray"),
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
            } else {
                // Make sure we're getting an item view
                NcAssert.True (PLACEHOLDER_TAG != view.Tag);
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
            view.OnClick = (int tag) => {
                switch (tag) {
                case SAVE_TAG:
                    onSaveButtonClicked (view);
                    break;
                case DEFER_TAG:
                    onDeferButtonClicked (view);
                    break;
                case ARCHIVE_TAG:
                    onArchiveButtonClicked (view);
                    break;
                case DELETE_TAG:
                    onDeleteButtonClicked (view);
                    break;
                default:
                    var message = String.Format ("Unknown action tag {0}", tag);
                    throw new NcAssert.NachoDefaultCaseFailure (message);
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
                    owner.carouselView.ScrollEnabled = false;
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

            var unavailableLabelView = new UILabel (new RectangleF (0, 15, viewWidth, 20));
            unavailableLabelView.Font = A.Font_AvenirNextRegular14;
            unavailableLabelView.TextColor = A.Color_NachoDarkText;
            unavailableLabelView.TextAlignment = UITextAlignment.Center;
            unavailableLabelView.Tag = UNAVAILABLE_TAG;
            view.AddSubview (unavailableLabelView);

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

            var messageHeaderView = new MessageHeaderView (new RectangleF (65, 15, viewWidth - 65 - 15, 60));
            messageHeaderView.CreateView ();
            messageHeaderView.Tag = MESSAGE_HEADER_TAG;
            messageHeaderView.SetAllBackgroundColors (UIColor.White);
            view.AddSubview (messageHeaderView);

            // Reminder image view
            var reminderImageView = new UIImageView (new RectangleF (65, 75 + 4, 12, 12));
            reminderImageView.Image = UIImage.FromBundle ("inbox-icn-deadline");
            reminderImageView.Tag = REMINDER_ICON_TAG;
            view.AddSubview (reminderImageView);

            // Reminder label view
            var reminderLabelView = new UILabel (new RectangleF (87, 75, 230, 20));
            reminderLabelView.Font = A.Font_AvenirNextRegular14;
            reminderLabelView.TextColor = A.Color_9B9B9B;
            reminderLabelView.Tag = REMINDER_TEXT_TAG;
            view.AddSubview (reminderLabelView);

            // Preview label view
            // Size fields will be recalculated after text is known
            var previewLabelView = BodyView.FixedSizeBodyView (new RectangleF (12, 70, viewWidth - 15 - 12, view.Frame.Height - 128));
            previewLabelView.Tag = PREVIEW_TAG;
            view.AddSubview (previewLabelView);

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

            // more icon view
            var moreView = new UIView (new RectangleF (17, frame.Height - 44 - 14 - 13, 18, 10));
            moreView.BackgroundColor = UIColor.White;
            moreView.Layer.CornerRadius = 2;
            view.AddSubview (moreView);

            var moreImageView = new UIImageView (new RectangleF (18, frame.Height - 44 - 14 - 16, 16, 16));
            moreImageView.Image = UIImage.FromBundle ("gen-readmore");
            moreImageView.Tag = USER_MORE_TAG;
            view.AddSubview (moreImageView);

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
            if (null == message) {
                return;
            }
            NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
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
            if (null == message) {
                return;
            }
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
            if (null == message) {
                return;
            }
            NcEmailArchiver.Delete (message);
        }

        protected void HandleUnavailableMessage (UIView view)
        {
            foreach (var s in view.Subviews) {
                s.Hidden = true;
            }
            var unavailableLabelView = view.ViewWithTag (UNAVAILABLE_TAG) as UILabel;
            unavailableLabelView.Text = "This message is unavailable";
            unavailableLabelView.Hidden = false;
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

            var unavailableLabelView = view.ViewWithTag (UNAVAILABLE_TAG) as UILabel;
            unavailableLabelView.Hidden = true;

            var viewWidth = view.Frame.Width;

            // User image view
            var userImageView = (UIImageView) view.ViewWithTag (USER_IMAGE_TAG);
            var userLabelView = (UILabel) view.ViewWithTag (USER_LABEL_TAG);
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

            var messageHeaderView = view.ViewWithTag (MESSAGE_HEADER_TAG) as MessageHeaderView;
            messageHeaderView.ConfigureView (message);

            messageHeaderView.OnClickChili = (object sender, EventArgs e) => {
                NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
                messageHeaderView.ConfigureView (message);
            };

            float previewViewAdjustment = 0;

            // Reminder image view and label
            var reminderImageView = view.ViewWithTag (REMINDER_ICON_TAG) as UIImageView;
            var reminderLabelView = view.ViewWithTag (REMINDER_TEXT_TAG) as UILabel;
            if (message.HasDueDate () || message.IsDeferred ()) {
                reminderImageView.Hidden = false;
                reminderLabelView.Hidden = false;
                reminderLabelView.Text = Pretty.ReminderText (message);
                previewViewAdjustment = 24;
            } else {
                reminderImageView.Hidden = true;
                reminderLabelView.Hidden = true;
            }

            // Size of preview, depends on reminder view
            var previewViewHeight = view.Frame.Height - 80 - previewViewAdjustment;
            previewViewHeight -= 44; // toolbar
            previewViewHeight -= 4; // padding

            var previewView = view.ViewWithTag (PREVIEW_TAG) as BodyView;
            previewView.Configure (message);
            previewView.Resize (new RectangleF (12, 70 + previewViewAdjustment, previewView.Frame.Width, previewViewHeight));
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
                v.Tag = PLACEHOLDER_TAG;
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
            NcAssert.True (PLACEHOLDER_TAG == view.Tag);
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
            if ((0 > index) || (carousel.NumberOfItems <= index)) {
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
                return 3.0f;
            default:
                return value;
            }

        }

        public override void CarouselWillBeginDragging (iCarousel carousel)
        {
            Log.Info (Log.LOG_UI, "DraggingStarted");
            NachoClient.Util.HighPriority ();
        }

        public override void CarouselDidEndDragging (iCarousel carousel, bool decelerate)
        {
            Log.Info (Log.LOG_UI, "DraggingEnded");
            NachoClient.Util.RegularPriority ();
        }

        public override void CarouselCurrentItemIndexDidChange (iCarousel carousel)
        {
            var previewLabelView =
                carousel.CurrentItemView.ViewWithTag (HotListCarouselDataSource.PREVIEW_TAG) as BodyView;
            if (null == previewLabelView) {
                return;
            }
            previewLabelView.PrioritizeBodyDownload ();
        }
    }
}

