// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public partial class NachoNowViewController : NcUIViewController, IMessageTableViewSourceDelegate, INachoMessageEditorParent, INachoFolderChooserParent, INachoCalendarItemEditorParent, ICalendarTableViewSourceDelegate, INachoDateControllerParent
    {
        protected bool priorityInboxNeedsRefresh;
        protected INachoEmailMessages priorityInbox;
        protected HotListTableViewSource hotListSource;

        protected UITableView hotListView;
        protected HotEventView hotEventView;

        public NachoNowViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            priorityInbox = NcEmailManager.PriorityInbox ();

            CreateView ();

            hotListSource = new HotListTableViewSource (this, priorityInbox);
            hotListView.Source = hotListSource;
        }

        protected void CreateView ()
        {
            if (null != NavigationItem) {
                NavigationItem.SetHidesBackButton (true, false);
            }

            var composeButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (composeButton, "contact-newemail");
            composeButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToCompose", new SegueHolder (null));
            };

            var newMeetingButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (newMeetingButton, "cal-add");
            newMeetingButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("NachoNowToEditEventView", new SegueHolder (null));
            };

            NavigationItem.Title = "Nacho Now";
                
            NavigationItem.LeftBarButtonItem = null;
            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] { composeButton, newMeetingButton };

            hotListView = new UITableView (carouselNormalSize (), UITableViewStyle.Plain);
            hotListView.BackgroundColor = A.Color_NachoBackgroundGray;
            hotListView.TableFooterView = new UIView (new RectangleF (0, 0, 1, 20));
            hotListView.TableFooterView.BackgroundColor = A.Color_NachoBackgroundGray;
            hotListView.DecelerationRate = UIScrollView.DecelerationRateFast;
            View.AddSubview (hotListView);

            hotEventView = new HotEventView (new RectangleF (0, 0, View.Frame.Width, 69));
            View.AddSubview (hotEventView);

            hotEventView.OnClick = ((int tag, int eventId) => {
                switch (tag) {
                case HotEventView.DIAL_IN_TAG:
                    // FIXME
                    break;
                case HotEventView.NAVIGATE_TO_TAG:
                    // FIXME
                    break;
                case HotEventView.LATE_TAG:
                    SendRunningLateMessage (eventId);
                    break;
                case HotEventView.FORWARD_TAG:
                    // FIXME
                    break;
                case HotEventView.OPEN_TAG:
                    var e = McEvent.QueryById<McEvent> (eventId);
                    if (null != e) {
                        PerformSegue ("NachoNowToEventView", new SegueHolder (e));
                    }
                    break;
                }
            });

            View.BackgroundColor = A.Color_NachoBackgroundGray;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            MaybeRefreshPriorityInbox ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        // Called from NachoTabBarController
        // if we need to handle a notification.
        public void HandleNotifications ()
        {
            // If we have a pending notification, bring up the event detail view
            var eventNotifications = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EventNotificationKey);
            var eventNotification = eventNotifications.FirstOrDefault ();
            if (null != eventNotification) {
                // TODO: Multi-account switch
                // var accountId = int.Parse(notification.Key);
                var eventId = int.Parse (eventNotification.Value);
                var e = McEvent.QueryById<McEvent> (eventId);
                eventNotification.Delete ();
                if (null != e) {
                    PerformSegue ("NachoNowToEventView", new SegueHolder (e));
                }
            }
            var emailNotifications = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EmailNotificationKey);
            var emailNotification = emailNotifications.FirstOrDefault ();
            if (null != emailNotification) {
                // TODO: Multi-account switch
                // var accountId = int.Parse (emailNotification.Key);
                var messageId = int.Parse (emailNotification.Value);
                emailNotification.Delete ();
                var t = new McEmailMessageThread ();
                var m = new NcEmailMessageIndex ();
                m.Id = messageId;
                t.Add (m);
                PerformSegue ("NachoNowToMessageView", new SegueHolder (t));
                return;
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "NachoNowToCalendar") {
                return; // Nothing to do
            }
            if (segue.Identifier == "NachoNowToEditEventView") {
                var vc = (EditEventViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var c = holder.value as McCalendar;
                vc.SetCalendarItem (c);
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "NachoNowToEventView") {
                var vc = (EventViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var e = holder.value as McEvent;
                vc.SetCalendarItem (e);
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
            if (segue.Identifier.Equals ("CalendarToEmailCompose")) {
                var dc = (MessageComposeViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var c = holder.value as McCalendar;
                dc.SetEmailPresetFields (new NcEmailAddress (NcEmailAddress.Kind.To, c.OrganizerEmail), c.Subject, "Running late");
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
                var holder = (SegueHolder)sender;
                var thread = (McEmailMessageThread)holder.value;
                var vc = (INachoDateController)segue.DestinationViewController;
                vc.Setup (this, thread, DateControllerType.Defer);
                return;
            }
            if (segue.Identifier == "NachoNowToFolders") {
                var vc = (INachoFolderChooser)segue.DestinationViewController;
                var h = sender as SegueHolder;
                vc.SetOwner (this, true, h);
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                RefreshPriorityInboxIfVisible ();

            }
            if (NcResult.SubKindEnum.Info_EmailMessageScoreUpdated == s.Status.SubKind) {
                RefreshPriorityInboxIfVisible ();
            }
        }

        protected void RefreshPriorityInboxIfVisible ()
        {
            priorityInboxNeedsRefresh = true;
            if (!this.IsVisible ()) {
                return;
            }
            MaybeRefreshPriorityInbox ();
        }

        protected void MaybeRefreshPriorityInbox ()
        {
            bool callReconfigure = true;

            if (priorityInboxNeedsRefresh) {
                priorityInboxNeedsRefresh = false;
                if (priorityInbox.Refresh ()) {
                    hotListView.ReloadData ();
                    callReconfigure = false;
                }
            }
            if (callReconfigure) {
                hotListSource.ReconfigureVisibleCells (hotListView);
            }
        }

        /// <summary>
        /// Show event, inbox, and hot list
        /// </summary>
        protected void LayoutView ()
        {
            hotListView.Frame = carouselNormalSize ();
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

        ///  IMessageTableViewSourceDelegate
        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
        }

        // ICalendarTableViewSourceDelegate
        public void SendRunningLateMessage (int eventId)
        {
            var e = McEvent.QueryById<McEvent> (eventId);
            if (null == e) {
                return;  // may be deleted
            }
            var c = McCalendar.QueryById<McCalendar> (e.CalendarId);
            if (null == c) {
                return; // may be deleted
            }
            PerformSegue ("CalendarToEmailCompose", new SegueHolder (c));
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

        ///  IMessageTableViewSourceDelegate
        public void MultiSelectToggle (MessageTableViewSource source, bool enabled)
        {
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, null);
        }

        public void DateSelected (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate)
        {
            NcMessageDeferral.DeferThread (thread, request, selectedDate);
        }

        public void DismissChildDateController (INachoDateController vc)
        {
            vc.Setup (null, null, DateControllerType.None);
            vc.DismissDateController (false, null);
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
            vc.SetOwner (null, false, null);
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

    }
}
