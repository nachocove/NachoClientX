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


        float startingX;
        float xOffset;

        UIView colorView;
        UIImageView swipingView;

        double percentageSlide (UIView view)
        {
            return (xOffset - startingX) / view.Frame.Width;
        }

        /// <summary>
        /// Gesture handler for date dots pan/swipe
        /// </summary>
        private void PanHandler (UIView view, UIPanGestureRecognizer obj)
        {
            Console.WriteLine ("PanHandler");
            if (UIGestureRecognizerState.Began == obj.State) {
                owner.carouselView.ScrollEnabled = false;
                startingX = obj.TranslationInView (view).X;
                colorView = new UIView (view.Frame);
                colorView.BackgroundColor = UIColor.Brown;
                colorView.Layer.CornerRadius = 5;
                var Image = Util.captureView (view);
                swipingView = new UIImageView (Image);
                view.Superview.AddSubview (colorView);
                view.Superview.AddSubview (swipingView);
                return;
            }

            if (UIGestureRecognizerState.Changed == obj.State) {
                xOffset = obj.TranslationInView (view).X;
                var frame = swipingView.Frame;
                frame.X = xOffset;
                swipingView.Frame = frame;
                var percentSlide = percentageSlide (view);
                Console.WriteLine ("percentSlide = {0}", percentSlide);
                if (-0.30 > percentSlide) {
                    colorView.BackgroundColor = new UIColor (232.0f / 255.0f, 61.0f / 255.0f, 14.0f / 255.0f, 1.0f); // red
                } else if (0 > percentSlide) {
                    colorView.BackgroundColor = new UIColor (85.0f / 255.0f, 213.0f / 255.0f, 80.0f / 255.0f, 1.0f); // green
                } else if (0.25 > percentSlide) {
                    colorView.BackgroundColor = new UIColor (206.0f / 255.0f, 149.0f / 255.0f, 98.0f / 255.0f, 1.0f); // brown
                } else {
                    colorView.BackgroundColor = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f); // yellow
                }
                return;
            }

            if ((UIGestureRecognizerState.Ended == obj.State) || (UIGestureRecognizerState.Cancelled == obj.State)) {
                owner.carouselView.ScrollEnabled = true;
                if (null != colorView) {
                    colorView.RemoveFromSuperview ();
                    colorView = null;
                }
                if (null != swipingView) {
                    UIView.Animate (0.1, () => {
                        var frame = swipingView.Frame;
                        frame.X = 0;
                        swipingView.Frame = frame;
                    }, () => {
                        swipingView.RemoveFromSuperview ();
                        swipingView = null;
                    });
                }
                var percentSlide = percentageSlide (view);
                if (-0.30 > percentSlide) {
                    onDeleteButtonClicked (view);
                } else if (-0.05 > percentSlide) {
                    onArchiveButtonClicked (view);
                } else if (0.05 > percentSlide) {
                    // ignore a tiny swipe
                } else if (0.30 > percentSlide) {
                    onSaveButtonClicked (view);
                } else { 
                    onDeferButtonClicked (view);
                }
                return;
            }
        }

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
            var view = new UIView (frame);
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
            previewLabelView.OnRenderStart = () => {
            };
            previewLabelView.OnRenderComplete = () => {
            };
            previewLabelView.OnDownloadStart = () => {
            };
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

            var replyButton = new UIBarButtonItem ();
            Util.SetOriginalImageForButton (replyButton, "toolbar-icn-reply");
            replyButton.Clicked += (object sender, EventArgs e) => {
//                ReplyActionSheet (view);
                onReplyButtonClicked (view, MessageComposeViewController.REPLY_ACTION);
            };
            preventBarButtonGC.Add (replyButton);

            var replyAllButton = new UIBarButtonItem ();
            Util.SetOriginalImageForButton (replyAllButton, "toolbar-icn-reply-all");
            replyAllButton.Clicked += (object sender, EventArgs e) => {
//                ReplyActionSheet (view);
                onReplyButtonClicked (view, MessageComposeViewController.REPLY_ALL_ACTION);
            };
            preventBarButtonGC.Add (replyAllButton);

            var forwardButton = new UIBarButtonItem ();
            Util.SetOriginalImageForButton (forwardButton, "toolbar-icn-fwd");
            forwardButton.Clicked += (object sender, EventArgs e) => {
//                ReplyActionSheet (view);
                onReplyButtonClicked (view, MessageComposeViewController.FORWARD_ACTION);
            };
            preventBarButtonGC.Add (forwardButton);

            var chiliButton = new UIBarButtonItem ();
            Util.SetOriginalImageForButton (chiliButton, "icn-nothot-gray");
            chiliButton.Clicked += (object sender, EventArgs e) => {
                onChiliButtonClicked (view);
            };
            preventBarButtonGC.Add (chiliButton);

            var deferButton = new UIBarButtonItem ();
            Util.SetOriginalImageForButton (deferButton, "email-defer-gray");
            deferButton.Clicked += (object sender, EventArgs e) => {
                onDeferButtonClicked (view);
            };
            preventBarButtonGC.Add (deferButton);

            var saveButton = new UIBarButtonItem ();
            Util.SetOriginalImageForButton (saveButton, "email-fileinfolder-gray");
            saveButton.Clicked += (object sender, EventArgs e) => {
                onSaveButtonClicked (view);
            };
            preventBarButtonGC.Add (saveButton);

            var archiveButton = new UIBarButtonItem ();
            Util.SetOriginalImageForButton (archiveButton, "email-archive-gray");
            archiveButton.Clicked += (object sender, EventArgs e) => {
                onArchiveButtonClicked (view);
            };
            preventBarButtonGC.Add (saveButton);

            var flexibleSpace = new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace);
            preventBarButtonGC.Add (flexibleSpace);

            var fixedSpace = new UIBarButtonItem (UIBarButtonSystemItem.FixedSpace);
            fixedSpace.Width = 25;
            preventBarButtonGC.Add (fixedSpace);

            var deleteButton = new UIBarButtonItem ();
            Util.SetOriginalImageForButton (deleteButton, "email-delete-gray");
            deleteButton.Clicked += (object sender, EventArgs e) => {
                onDeleteButtonClicked (view);
            };
            preventBarButtonGC.Add (deleteButton);

            var toolbar = new UIToolbar (new RectangleF (0, frame.Height - 44, frame.Width, 44));
            toolbar.SetItems (new UIBarButtonItem[] {
                replyButton,
                fixedSpace,
                replyAllButton,
                fixedSpace,
                forwardButton,
                flexibleSpace,
//                chiliButton,
//                flexibleSpace,
//                deferButton,
                flexibleSpace,
                archiveButton,
//                flexibleSpace,
//                saveButton,
                fixedSpace,
                deleteButton
            }, false);
            view.AddSubview (toolbar);

            var gestureRecognizer = new UIPanGestureRecognizer ((UIPanGestureRecognizer obj) => {
                PanHandler (view, obj);
            });
            gestureRecognizer.ShouldRecognizeSimultaneously = delegate {
                return true;
            };
            gestureRecognizer.ShouldBegin = delegate(UIGestureRecognizer obj) {
                var Recognizer = (UIPanGestureRecognizer)obj;
                var Velocity = Recognizer.VelocityInView (view);
                return Math.Abs (Velocity.X) > Math.Abs (Velocity.Y);
            };

            view.AddGestureRecognizer (gestureRecognizer);

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

        /// <summary>
        /// Populate message cells with data, adjust sizes and visibility
        /// </summary>
        protected void ConfigureView (UIView view, int messageThreadIndex)
        {           
            // Save thread index
            view.Tag = messageThreadIndex;
            var messageThread = owner.priorityInbox.GetEmailThread (messageThreadIndex);

            if (null == messageThread) {
                foreach (var s in view.Subviews) {
                    s.Hidden = true;
                }
                var slv = view.ViewWithTag (SUBJECT_TAG) as UILabel;
                slv.Text = "This message is unavailable.";
                slv.Hidden = false;
                return;
            }

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
                    onReplyButtonClicked (view, MessageComposeViewController.REPLY_ACTION);
                    break;
                case 1:
                    onReplyButtonClicked (view, MessageComposeViewController.REPLY_ALL_ACTION);
                    break;
                case 2:
                    onReplyButtonClicked (view, MessageComposeViewController.FORWARD_ACTION);
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

