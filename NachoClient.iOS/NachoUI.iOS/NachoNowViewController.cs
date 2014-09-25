// This file has been autogenerated from a class added in the UI designer.

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
    public partial class NachoNowViewController : NcUIViewController, INachoMessageEditorParent, INachoFolderChooserParent, INachoCalendarItemEditorParent, ICalendarTableViewSourceDelegate
    {
        public bool wrap = false;
        public INachoEmailMessages priorityInbox;
        protected CalendarTableViewSource calendarSource;
        UITapGestureRecognizer carouselTapGestureRecognizer = null;

        public iCarousel carouselView;
        protected UITableView calendarTableView;

        public NachoNowViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            priorityInbox = NcEmailManager.PriorityInbox ();

            calendarSource = new CalendarTableViewSource ();
            calendarSource.owner = this;
            calendarSource.SetCalendar (NcEventManager.Instance);

            CreateView ();

            // configure carousel
            carouselView.DataSource = new HotListCarouselDataSource (this);
            carouselView.Delegate = new HotListCarouselDelegate (this);  
        }

        protected void CreateView ()
        {
            var composeButton = new UIBarButtonItem ();
            Util.SetOriginalImageForButton (composeButton, "contact-newemail");
            composeButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToCompose", new SegueHolder (null));
            };

            var newMeetingButton = new UIBarButtonItem ();
            Util.SetOriginalImageForButton (newMeetingButton, "cal-add");
            newMeetingButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToEditEventView", new SegueHolder (null));
            };
                
            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] { composeButton };
            NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { newMeetingButton };

            carouselView = new iCarousel ();
            carouselView.Frame = carouselNormalSize ();
            carouselView.Type = iCarouselType.Linear;
            carouselView.Vertical = true;
            carouselView.ContentOffset = new SizeF (0f, 0f);
            carouselView.BackgroundColor = UIColor.Clear;
            carouselView.IgnorePerpendicularSwipes = true;
            View.AddSubview (carouselView);

            carouselTapGestureRecognizer = new UITapGestureRecognizer ();
            carouselTapGestureRecognizer.NumberOfTapsRequired = 2;
            carouselTapGestureRecognizer.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("CarouselTapSelector:"));
            carouselTapGestureRecognizer.ShouldRecognizeSimultaneously = delegate {
                return true;
            };
            carouselTapGestureRecognizer.ShouldReceiveTouch += (UIGestureRecognizer r, UITouch t) => {
                if (t.View is UIControl) {
                    return false;
                } else {
                    return true;
                }
            };
            carouselTapGestureRecognizer.Enabled = true;
            carouselView.AddGestureRecognizer (carouselTapGestureRecognizer);

            calendarTableView = new UITableView ();
            calendarTableView.SeparatorStyle = UITableViewCellSeparatorStyle.SingleLine;
            calendarTableView.Source = calendarSource;
            View.AddSubview (calendarTableView);

            View.BackgroundColor = A.Color_NachoBackgroundGray;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        ///        NachoNowToCalendar(null)
        ///        NachoNowToCalendarItem (index path)
        ///        NachoNowToCompose (null)
        ///        NachoNowToContacts (null)
        ///        NachoNowToMessageAction (index path)
        ///        NachoNowToMessageList (inbox folder)
        ///        NachoNowToMessageList(deferred folder)
        ///        NachoNowToMessagePriority  (index path)
        ///        NachoNowToMessageView (index path)
        ///
        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "NachoNowToCalendar") {
                return; // Nothing to do
            }
            if (segue.Identifier == "NachoNowToEditEventView") {
                var vc = (EditEventViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var c = holder.value as McCalendar;
                if (null == c) { 
                    vc.SetCalendarItem (null, CalendarItemEditorAction.create);
                } else {
                    vc.SetCalendarItem (c, CalendarItemEditorAction.create);
                }
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "NachoNowToEventView") {
                var vc = (EventViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var e = holder.value as McEvent;
                vc.SetCalendarItem (e, CalendarItemEditorAction.view);
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "NachoNowToCompose") {
                var vc = (MessageComposeViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                if (null == h) {
                    // Composing a message
                    vc.SetAction (null, null);
                } else {
                    vc.SetAction ((McEmailMessageThread)h.value2, (string)h.value);
                }
                vc.SetOwner (this);
                return;
            }

            if (segue.Identifier == "NachoNowToMessageList") {
                var holder = (SegueHolder)sender;
                var messageList = (INachoEmailMessages)holder.value;
                var messageListViewController = (MessageListViewController)segue.DestinationViewController;
                messageListViewController.SetEmailMessages (messageList);
                return;
            }
            if (segue.Identifier == "NachoNowToMessageView") {
                var vc = (MessageViewController)segue.DestinationViewController;
                var holder = (SegueHolder)sender;
                vc.thread = holder.value as McEmailMessageThread;                
                return;
            }
            if (segue.Identifier == "NachoNowToMessagePriority") {
                var vc = (MessagePriorityViewController)segue.DestinationViewController;
                var holder = (SegueHolder)sender;
                vc.thread = holder.value as McEmailMessageThread;
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "NachoNowToMessageAction") {
                var vc = (MessageActionViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                vc.SetOwner (this, h);
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                priorityInbox.Refresh ();
                carouselView.ReloadData ();
            }
            if (NcResult.SubKindEnum.Info_CalendarSetChanged == s.Status.SubKind) {
                calendarSource.Refresh ();
                calendarTableView.ReloadData ();
            }
            if (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated == s.Status.SubKind) {
                priorityInbox.Refresh ();
                carouselView.ReloadData ();
            }
            if (NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded == s.Status.SubKind) {
                ProcessDownloadComplete (true);
            }
            if (NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed == s.Status.SubKind) {
                ProcessDownloadComplete (false);
            }
        }

        private void ProcessDownloadComplete (bool succeed)
        {
            var bodyView = carouselView.CurrentItemView.ViewWithTag (HotListCarouselDataSource.PREVIEW_TAG) as BodyView;
            // To avoid unnecessary reload, we only reload if the current item was downloading
            // and the body is now completely downloaded.
            if (!bodyView.IsDownloadComplete ()) {
                return;
            }
            bodyView.DownloadComplete (succeed);
            carouselView.ReloadItemAtIndex (carouselView.CurrentItemIndex, true);
        }

        /// <summary>
        /// Show event, inbox, and hot list
        /// </summary>
        protected void LayoutView ()
        {
            calendarSource.SetCompactMode (true);
            calendarTableView.ScrollEnabled = false;
            calendarTableView.Frame = calendarSmallSize ();
            calendarTableView.ReloadData ();
            calendarSource.ScrollToNearestEvent (calendarTableView, DateTime.UtcNow, 7);

            carouselView.Frame = carouselNormalSize ();
            carouselView.Alpha = 1.0f;
            carouselView.ClipsToBounds = true;
        }

        public override void ViewDidLayoutSubviews ()
        {
            base.ViewDidLayoutSubviews ();
            LayoutView ();
        }

        int CALENDAR_VIEW_HEIGHT = (69);

        protected RectangleF carouselNormalSize ()
        {
            var rect = new RectangleF (0, CALENDAR_VIEW_HEIGHT, View.Frame.Width, View.Frame.Height - CALENDAR_VIEW_HEIGHT);
            return rect;
        }

        /// Grows from top of View
        protected RectangleF calendarSmallSize ()
        {
            var parentFrame = View.Frame;
            var inboxFrame = new RectangleF ();
            inboxFrame.Y = 0;
            inboxFrame.Height = CALENDAR_VIEW_HEIGHT;
            inboxFrame.X = parentFrame.X;
            inboxFrame.Width = parentFrame.Width;
            return inboxFrame;
        }

  

        [MonoTouch.Foundation.Export ("CarouselTapSelector:")]
        public void OnDoubleTapCarousel (UIGestureRecognizer sender)
        {
            // FIXME: What to do on double tap?
        }

        ///  IMessageTableViewSourceDelegate
        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
        }

        // ICalendarTableViewSourceDelegate
        public void SendRunningLateMessage (int calendarIndex)
        {
            NcAssert.CaseError ();
        }

        // ICalendarTableViewSourceDelegate
        public void CalendarTableViewScrollingEnded ()
        {
        }

        ///  IMessageTableViewSourceDelegate
        public void MessageThreadSelected (McEmailMessageThread messageThread)
        {
            PerformSegue ("NachoNowToMessageList", new SegueHolder (NcEmailManager.Inbox ()));
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, null);
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var m = thread.SingleMessageSpecialCase ();
            var t = CalendarHelper.CreateTask (m);
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                PerformSegue ("", new SegueHolder (t));
            }));
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var m = thread.SingleMessageSpecialCase ();
            var c = CalendarHelper.CreateMeeting (m);
            vc.DismissMessageEditor (false, new NSAction (delegate {
                PerformSegue ("NachoNowToEditEventView", new SegueHolder (c));
            }));
        }

        /// <summary>
        /// INachoFolderChooser Delegate
        /// </summary>
        public void DismissChildFolderChooser (INachoFolderChooser vc)
        {
            vc.SetOwner (null, null);
            vc.DismissFolderChooser (false, null);
        }

        /// <summary>
        /// INachoFolderChooser Delegate
        /// </summary>
        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            var segueHolder = (SegueHolder)cookie;
            var messageThread = (McEmailMessageThread)segueHolder.value;
            var message = messageThread.SingleMessageSpecialCase ();
            NcEmailArchiver.Move (message, folder);
            vc.DismissFolderChooser (true, null);
        }

        /// <summary>
        /// INachoCalendarItemEditorParent delegate
        /// </summary>
        public void DismissChildCalendarItemEditor (INachoCalendarItemEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissCalendarItemEditor (false, null);
        }

        public void ReloadHotListData ()
        {
            carouselView.ReloadData ();
        }
    }
}
