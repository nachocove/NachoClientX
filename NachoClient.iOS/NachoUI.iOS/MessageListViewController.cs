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
using MCSwipeTableViewCellBinding;
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public partial class MessageListViewController : NcUITableViewController, IUISearchDisplayDelegate, IUISearchBarDelegate, INachoMessageEditorParent, INachoCalendarItemEditorParent, INachoFolderChooserParent, IMessageTableViewSourceDelegate, INachoDateControllerParent
    {
        MessageTableViewSource messageSource;
        protected UIBarButtonItem composeMailButton;
        protected UIBarButtonItem cancelSelectedButton;
        protected UIBarButtonItem moreSelectedButton;

        protected const string UICellReuseIdentifier = "UICell";
        protected const string EmailMessageReuseIdentifier = "EmailMessage";

        protected HashSet<int> MultiSelect = null;

        protected NcCapture ReloadCapture;
        private string ReloadCaptureName;

        protected const int BLOCK_MENU_TAG = 1000;

        public void SetEmailMessages (INachoEmailMessages messageThreads)
        {
            this.messageSource.SetEmailMessages (messageThreads);
        }

        public MessageListViewController (IntPtr handle) : base (handle)
        {
            MultiSelect = new HashSet<int> ();
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

            cancelSelectedButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (cancelSelectedButton, "gen-close");
            cancelSelectedButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectCancel (TableView);
            };

            moreSelectedButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (moreSelectedButton, "gen-more");
            moreSelectedButton.Clicked += (object sender, EventArgs e) => {
                UIBlockMenu bm = (UIBlockMenu)View.ViewWithTag (BLOCK_MENU_TAG);
                TableView.ScrollEnabled = false;
                bm.MenuTapped (View.Bounds);
            };

            UIBlockMenu blockMenu = new UIBlockMenu (this, new List<UIBlockMenu.Block> () {
                new UIBlockMenu.Block ("contact-quickemail", "Delete", () => {
                    if (null != messageSource) {
                        messageSource.MultiSelectDelete (TableView);
                    }
                }),
                new UIBlockMenu.Block ("email-calendartime", "Move to folder", () => {
                    var h = new SegueHolder (TableView);
                    PerformSegue ("MessageListToFolders", h);
                }),
                new UIBlockMenu.Block ("now-addcalevent", "Archive", () => {
                    if (null != messageSource) {
                        messageSource.MultiSelectArchive (TableView);
                    }
                })
            }, View.Frame.Width);

            blockMenu.Tag = BLOCK_MENU_TAG;
            View.AddSubview (blockMenu);

            blockMenu.MenuWillDisappear += (object sender, EventArgs e) => {
                TableView.ScrollEnabled = true;
            };

            TableView.SeparatorColor = A.Color_NachoBorderGray;
            NavigationController.NavigationBar.Translucent = false;
            Util.HideBlackNavigationControllerLine (NavigationController.NavigationBar);

            ReloadDataMaintainingPosition ();

            UIView backgroundView = new UIView (new RectangleF (0, 0, 320, 480));
            backgroundView.BackgroundColor = new UIColor (227f / 255f, 227f / 255f, 227f / 255f, 1.0f);
            TableView.BackgroundView = backgroundView;

            messageSource.owner = this;
            TableView.Source = messageSource;

            MultiSelectToggle (messageSource, false);

            TableView.TableHeaderView = null; // beta 1
        }

        public void MultiSelectToggle (MessageTableViewSource source, bool enabled)
        {
            if (enabled) {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    moreSelectedButton,
                };
                NavigationItem.SetLeftBarButtonItem (cancelSelectedButton, false);
                NavigationItem.HidesBackButton = true;

            } else {
                NavigationItem.RightBarButtonItem = null;
                NavigationItem.RightBarButtonItem = composeMailButton; /* beta 1 searchButton */ 
                NavigationItem.LeftBarButtonItem = null;
                NavigationItem.HidesBackButton = false;
            }
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
            if (messageSource.RefreshEmailMessages ()) {
                ReloadCapture.Start ();
                TableView.ReloadData ();
                ReloadCapture.Stop ();
            }
            NachoCore.Utils.NcAbate.RegularPriority ("MessageListViewController ReloadDataMaintainingPosition");
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
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
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback: EmailMessageSetChanged");
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
            vc.DimissDateController (false, null);
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
                PerformSegue ("NachoNowToEditEvent", new SegueHolder (c));
            }));
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

    }
}
 
