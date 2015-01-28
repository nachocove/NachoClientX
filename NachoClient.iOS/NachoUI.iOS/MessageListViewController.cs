// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;

//using MonoTouch.CoreGraphics;
using MonoTouch.UIKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public partial class MessageListViewController : NcUITableViewController, IUISearchDisplayDelegate, IUISearchBarDelegate, INachoMessageEditorParent, INachoCalendarItemEditorParent, INachoFolderChooserParent, IMessageTableViewSourceDelegate, INachoDateControllerParent
    {
        MessageTableViewSource messageSource;
        protected UIBarButtonItem composeMailButton;
        protected UIBarButtonItem multiSelectButton;
        protected UIBarButtonItem cancelSelectedButton;
        protected UIBarButtonItem archiveButton;
        protected UIBarButtonItem deleteButton;
        protected UIBarButtonItem moveButton;
        protected UIBarButtonItem backButton;

        protected const string UICellReuseIdentifier = "UICell";
        protected const string EmailMessageReuseIdentifier = "EmailMessage";

        protected NcCapture ReloadCapture;
        private string ReloadCaptureName;

        public void SetEmailMessages (INachoEmailMessages messageThreads)
        {
            this.messageSource.SetEmailMessages (messageThreads);
        }

        public MessageListViewController (IntPtr handle) : base (handle)
        {
            messageSource = new MessageTableViewSource ();
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            ReloadCaptureName = "MessageListViewController.Reload";
            NcCapture.AddKind (ReloadCaptureName);
            ReloadCapture = NcCapture.Create (ReloadCaptureName);

            composeMailButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (composeMailButton, "contact-newemail");
            composeMailButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageListToCompose", this);
            };

            multiSelectButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (multiSelectButton, "folder-edit");
            multiSelectButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectEnable (TableView);
            };

            cancelSelectedButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (cancelSelectedButton, "gen-close");
            cancelSelectedButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectCancel (TableView);
            };

            archiveButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (archiveButton, "gen-archive");
            archiveButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectArchive (TableView);
            };

            deleteButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (deleteButton, "gen-delete-all");
            deleteButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectDelete (TableView);
            };

            moveButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (moveButton, "folder-move");
            moveButton.Clicked += (object sender, EventArgs e) => {
                var h = new SegueHolder (TableView);
                PerformSegue ("MessageListToFolders", h);
            };

            TableView.SeparatorColor = A.Color_NachoBorderGray;
            NavigationController.NavigationBar.Translucent = false;
            Util.HideBlackNavigationControllerLine (NavigationController.NavigationBar);

            View.BackgroundColor = new UIColor (227f / 255f, 227f / 255f, 227f / 255f, 1.0f);

            messageSource.owner = this;
            TableView.Source = messageSource;

            CustomizeBackButton ();
            MultiSelectToggle (messageSource, false);

            TableView.TableHeaderView = null; // beta 1

            RefreshControl = new UIRefreshControl ();
            RefreshControl.Hidden = true;
            RefreshControl.TintColor = A.Color_NachoGreen;
            RefreshControl.AttributedTitle = new NSAttributedString ("Refreshing...");
            RefreshControl.ValueChanged += (object sender, EventArgs e) => {
                RefreshControl.BeginRefreshing ();
                ReloadDataMaintainingPosition ();
                new NcTimer ("MessageListViewController refresh", refreshCallback, null, 2000, 0);
            };

            Util.ConfigureNavBar (false, this.NavigationController);
        }

        protected void refreshCallback (object sender)
        {
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                RefreshControl.EndRefreshing ();
            });
        }

        protected virtual void CustomizeBackButton ()
        {
        }

        public void MultiSelectToggle (MessageTableViewSource source, bool enabled)
        {
            if (enabled) {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    deleteButton,
                    moveButton,
                    archiveButton,
                };
                NavigationItem.HidesBackButton = true;
                NavigationItem.SetLeftBarButtonItem (cancelSelectedButton, false);
            } else {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    composeMailButton,
                    multiSelectButton,
                };
                if (null == backButton) {
                    NavigationItem.HidesBackButton = false;
                    NavigationItem.LeftBarButtonItem = null;
                } else {
                    NavigationItem.HidesBackButton = true;
                    NavigationItem.LeftBarButtonItem = backButton;
                }
            }
        }

        public void MultiSelectChange (MessageTableViewSource source, int count)
        {
            archiveButton.Enabled = (count != 0);
            deleteButton.Enabled = (count != 0);
            moveButton.Enabled = (count != 0);
        }

        public int GetFirstVisibleRow ()
        {       
            var paths = TableView.IndexPathsForVisibleRows; // Must be on UI thread
            if (null == paths) {
                return -1;
            }
            var path = paths.FirstOrDefault ();
            if (null == path) {
                return -1;
            }
            return path.Row;
        }

        public void ReloadDataMaintainingPosition ()
        {
            NachoCore.Utils.NcAbate.HighPriority ("MessageListViewController ReloadDataMaintainingPosition");
            ReloadCapture.Start ();
            List<int> adds;
            List<int> deletes;
            if (messageSource.RefreshEmailMessages (out adds, out deletes)) {
                Util.UpdateTable (TableView, adds, deletes);
            } else {
                messageSource.ReconfigureVisibleCells (TableView);
            }
            ReloadCapture.Stop ();
            NachoCore.Utils.NcAbate.RegularPriority ("MessageListViewController ReloadDataMaintainingPosition");
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            // TODO: Figure this out
            // When this view is loaded directly from the tab bar,
            // the first time the view is displayed, the content
            // offset is set such that the refresh controller is
            // visible.  The second time this view is presented
            // the content offset is set to properly.
            if (0 > TableView.ContentOffset.Y) {
                TableView.ContentOffset = new PointF (0, 0);
            }

            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            NavigationItem.Title = messageSource.GetDisplayName ();

            ReloadDataMaintainingPosition ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            // In case we exit during scrolling
            NachoCore.Utils.NcAbate.RegularPriority ("MessageListViewController ViewWillDisappear");
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                ReloadDataMaintainingPosition ();
            }
            if (NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded == s.Status.SubKind) {
                ReloadDataMaintainingPosition ();
            }
            if (NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded == s.Status.SubKind) {
                ReloadDataMaintainingPosition ();
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            var blurry = segue.DestinationViewController as BlurryViewController;
            if (null != blurry) {
                blurry.CaptureView (this.View);
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
            if (segue.Identifier == "SegueToNachoNow") {
                return;
            }
            if (segue.Identifier == "MessageListToCompose") {
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
            if (segue.Identifier == "MessageListToFolders") {
                var vc = (INachoFolderChooser)segue.DestinationViewController;
                var h = sender as SegueHolder;
                vc.SetOwner (this, true, h);
                return;
            }
            if (segue.Identifier == "NachoNowToEditEvent") {
                var vc = (EditEventViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var e = holder.value as McCalendar;
                vc.SetCalendarItem (e);
                vc.SetOwner (this);
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        ///  IMessageTableViewSourceDelegate
        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
        }

        ///  IMessageTableViewSourceDelegate
        public void MessageThreadSelected (McEmailMessageThread messageThread)
        {
            PerformSegue ("NachoNowToMessageView", new SegueHolder (messageThread));
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
            if (null != m) {
                var t = CalendarHelper.CreateTask (m);
                vc.SetOwner (null);
                vc.DismissMessageEditor (false, new NSAction (delegate {
                    PerformSegue ("", new SegueHolder (t));
                }));
            }
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var m = thread.SingleMessageSpecialCase ();
            if (null != m) {
                var c = CalendarHelper.CreateMeeting (m);
                vc.DismissMessageEditor (false, new NSAction (delegate {
                    PerformSegue ("NachoNowToEditEvent", new SegueHolder (c));
                }));
            }
        }

        /// <summary>
        /// INachoCalendarItemEditorParent Delegate
        /// </summary>
        public void DismissChildCalendarItemEditor (INachoCalendarItemEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissCalendarItemEditor (true, null);
        }

        /// <summary>
        /// INachoFolderChooser Delegate
        /// </summary>
        public void DismissChildFolderChooser (INachoFolderChooser vc)
        {
            vc.SetOwner (null, false, null);
            vc.DismissFolderChooser (false, null);
        }

        public void SetParentCalendarItem (McEvent e)
        {
            NcAssert.CaseError();
        }

        /// <summary>
        /// INachoFolderChooser Delegate
        /// </summary>
        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            if (null != messageSource) {
                messageSource.FolderSelected (vc, folder, cookie);
            }
            vc.DismissFolderChooser (true, null);
        }

        protected void BackShouldSwitchToFolders ()
        {
            using (var image = UIImage.FromBundle ("nav-backarrow")) {
//                backButton = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, onClickBackButton);
                var button = UIButton.FromType (UIButtonType.System);
                button.Frame = new RectangleF (0, 0, 70, 30);
                button.SetTitle ("Mail", UIControlState.Normal);
                button.SetTitleColor (UIColor.White, UIControlState.Normal);
                button.SetImage (image, UIControlState.Normal);
                button.Font = UINavigationBar.Appearance.TitleTextAttributes.Font;
                backButton = new UIBarButtonItem (button);
                button.TouchUpInside += onClickBackButton;
            }
        }

        protected void onClickBackButton (object sender, EventArgs e)
        {
            var nachoTabBar = Util.GetActiveTabBar ();
            nachoTabBar.SwitchToFolders ();
        }
    }
}
