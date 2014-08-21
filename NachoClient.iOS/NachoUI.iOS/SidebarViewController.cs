// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using System.Drawing;
using MonoTouch.CoreAnimation;
using MonoTouch.CoreGraphics;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class SidebarViewController : NcUIViewController
    {
        ///   cellIDs for segues
        ///      "SidebarToAccounts"
        ///      "SidebarToCalendar"
        ///      "SidebarToContacts"
        ///      "SidebarToFolders"
        ///      "SidebarToSettings"
        ///      "SidebarToMessages"
        ///      "SidebarToDeferredMessages"
        ///      "SidebarToNachoNow"
        ///      "SidebarToHome"
        ///      "SidebarToFiles"

        const string SidebarToFoldersSegueId = "SidebarToFolders";
        const string SidebarToContactsSegueId = "SidebarToContacts";
        const string SidebarToCalendarSegueId = "SidebarToCalendar";
        const string SidebarToMessagesSegueId = "SidebarToMessages";
        const string SidebarToDeferredMessagesSegueId = "SidebarToDeferredMessages";
        const string SidebarToNachoNowSegueId = "SidebarToNachoNow";
        const string SidebarToHomeSegueId = "SidebarToHome";
        const string SidebarToAccountsSegueId = "SidebarToAccounts";
        const string SidebarToSettingsSegueId = "SidebarToSettings";
        const string SidebarToGeneralSettingsSegueId = "SidebarToGeneralSettings";
        const string SidebarToSupportSegueId = "SidebarToSupport";
        const string SidebarToFilesSegueId = "SidebarToFiles";
        const string SidebarToTasksSegueId = "SidebarToTasks";
        const string SidebarToNewEmailSegueId = "SidebarToNewEmail";
        const string SidebarToNewEventSegueId = "SidebarToEditEvent";
        const string SidebarToHotListSegueId = "SidebarToHotList";



        protected class ButtonInfo
        {
            public string label { get; set; }

            public string imageName { get; set; }

            public string segueIdentifier { get; set; }

            public ButtonInfo (string label, string imageName, string segueIdentifier)
            {
                this.label = label;
                this.imageName = imageName;
                this.segueIdentifier = segueIdentifier;
            }
        }

        protected UIView contentView;

        public SidebarViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
        }

        /// <summary>
        /// Update the list of folders when we appear
        /// </summary>
        /// <param name="animated">If set to <c>true</c> animated.</param>
        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }

            CreateView ();
        }

        const float BUTTON_SIZE = 60;
        const float BUTTON_LABEL_HEIGHT = 20;
        const float BUTTON_PADDING_HEIGHT = 25;
        const float BUTTON_PADDING_WIDTH = 20;

        protected void CreateView ()
        {
            contentView = new UIView (View.Frame);
            contentView.BackgroundColor = UIColor.Black;
            View.AddSubview (contentView);

            List<ButtonInfo> buttonInfoList = new List<ButtonInfo> (new ButtonInfo[] {
                new ButtonInfo ("Inbox", "menu-inbox", SidebarToMessagesSegueId),
                new ButtonInfo ("Calendar", "menu-calendar", SidebarToCalendarSegueId),
                new ButtonInfo ("Contacts", "menu-contacts", SidebarToContactsSegueId),
                new ButtonInfo (null, null, null),
                new ButtonInfo ("Hot List", "menu-chili", SidebarToHotListSegueId),
                new ButtonInfo ("New Email", "menu-new-email", SidebarToNewEmailSegueId),
                new ButtonInfo ("New Event", "menu-new-event", SidebarToNewEventSegueId),
                new ButtonInfo (null, null, null),
                new ButtonInfo ("Deferred", "menu-deferred", SidebarToDeferredMessagesSegueId),
                new ButtonInfo ("Files", "menu-attachments", SidebarToFilesSegueId),
                new ButtonInfo ("Folders", "menu-folders", SidebarToFoldersSegueId),
                new ButtonInfo (null, null, null),
                new ButtonInfo ("Settings", "menu-settings", SidebarToGeneralSettingsSegueId),
                null,
                new ButtonInfo ("Support", "menu-help", SidebarToSupportSegueId),
            });

            var center = contentView.Center;
            center.X = (320 / 2); // KLUDGE
            center.Y = center.Y;

            var xOffset = center.X - BUTTON_SIZE - BUTTON_PADDING_WIDTH;
            var yOffset = center.Y - (1.5F * BUTTON_PADDING_HEIGHT) - (2F * (BUTTON_SIZE + BUTTON_LABEL_HEIGHT)) + (0.5F * BUTTON_SIZE);

            foreach (var buttonInfo in buttonInfoList) {
                if (null == buttonInfo) {
                    xOffset += BUTTON_SIZE + BUTTON_PADDING_WIDTH;
                    continue;
                }
                if (null == buttonInfo.label) {
                    xOffset = center.X - BUTTON_SIZE - BUTTON_PADDING_WIDTH;
                    yOffset += BUTTON_SIZE + BUTTON_LABEL_HEIGHT + BUTTON_PADDING_HEIGHT;
                    continue;
                }
                var button = UIButton.FromType (UIButtonType.RoundedRect);
                button.Layer.CornerRadius = (BUTTON_SIZE / 2);
                button.Layer.MasksToBounds = true;
                button.Layer.BorderColor = UIColor.White.CGColor;
                button.Layer.BorderWidth = 1;
                button.Frame = new RectangleF (0, 0, BUTTON_SIZE, BUTTON_SIZE);
                button.Center = new PointF (xOffset, yOffset);
                button.SetImage (UIImage.FromBundle (buttonInfo.imageName), UIControlState.Normal);
                button.TintColor = UIColor.White;
                button.TouchUpInside += (object sender, EventArgs e) => {
                    var identifer = buttonInfo.segueIdentifier;
                    PerformSegue (identifer, new SegueHolder (null));
                };
                contentView.AddSubview (button);

                var label = new UILabel ();
                label.Text = buttonInfo.label;
                label.TextColor = A.Color_FFFFFF;
                label.Font = A.Font_AvenirNextRegular14;
                label.TextAlignment = UITextAlignment.Center;
                label.SizeToFit ();
                label.Center = new PointF (xOffset, 5 + yOffset + ((BUTTON_SIZE + BUTTON_LABEL_HEIGHT) / 2));
                contentView.AddSubview (label);

                xOffset += BUTTON_SIZE + BUTTON_PADDING_WIDTH;
            }

            var dismissLabel = new UILabel ();
            dismissLabel.Text = "Dismiss";
            dismissLabel.TextColor = A.Color_FFFFFF;
            dismissLabel.Font = A.Font_AvenirNextRegular12;
            dismissLabel.TextAlignment = UITextAlignment.Center;
            dismissLabel.SizeToFit ();
            dismissLabel.Center = new PointF (320 / 2, View.Frame.Height - dismissLabel.Frame.Height);
            contentView.AddSubview (dismissLabel);

            var tap = new UITapGestureRecognizer ((UITapGestureRecognizer obj) => {
                this.RevealViewController ().RevealToggleAnimated(true);
            });
            dismissLabel.AddGestureRecognizer (tap);
            dismissLabel.UserInteractionEnabled = true;
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            Log.Info (Log.LOG_UI, "PrepareForSegue: {0}", segue.Identifier);

            UIViewController destViewController = (UIViewController)segue.DestinationViewController;

            switch (segue.Identifier) {
            case SidebarToContactsSegueId:
                {
                    // ContactsViewController vc = (ContactsViewController)destViewController;
                }
                break;
            case SidebarToCalendarSegueId:
                {
                    CalendarViewController vc = (CalendarViewController)destViewController;
                    vc.UseDeviceCalendar = false;
                }
                break;
            case SidebarToMessagesSegueId:
                {
                    MessageListViewController vc = (MessageListViewController)destViewController;
                    var messageList = NcEmailManager.Inbox ();
                    vc.SetEmailMessages (messageList);
                }
                break;
            case SidebarToHotListSegueId:
                {
                    MessageListViewController vc = (MessageListViewController)destViewController;
                    var messageList = NcEmailManager.PriorityInbox ();
                    vc.SetEmailMessages (messageList);
                }
                break;
            case SidebarToDeferredMessagesSegueId:
                {
                    MessageListViewController vc = (MessageListViewController)destViewController;
                    var messageList = new NachoDeferredEmailMessages ();
                    vc.SetEmailMessages (messageList);
                }
                break;
            case SidebarToNewEventSegueId:
                {
                    var vc = (EditEventViewController)destViewController;
                    vc.SetCalendarItem (null, CalendarItemEditorAction.create);
                    vc.showMenu = true;
                }
                break;
            case SidebarToNewEmailSegueId:
                {
                    var vc = (MessageComposeViewController)destViewController;
                    vc.showMenu = true;
                }
                break;
            default:
                // No worries; nothing to send to destination view controller
                break;
            }

            if (segue.GetType () == typeof(SWRevealViewControllerSegue)) {
                Log.Info (Log.LOG_UI, "PrepareForSqueue: SWRevealViewControllerSegue");
                SWRevealViewControllerSegue swSegue = (SWRevealViewControllerSegue)segue;
                swSegue.PerformBlock = PerformBlock;
            } else {
                this.RevealViewController ().SetFrontViewPosition (FrontViewPosition.Left, true);
            }
        }

        /// <summary>
        /// Started from PrepareForSegue
        /// </summary>
        public void PerformBlock (SWRevealViewControllerSegue s, UIViewController svc, UIViewController dvc)
        {
            Log.Info (Log.LOG_UI, "PrepareForSegue: PerformBlock");
            UINavigationController navController = (UINavigationController)this.RevealViewController ().FrontViewController;
            navController.SetViewControllers (new UIViewController[] { dvc }, true);
            this.RevealViewController ().SetFrontViewPosition (FrontViewPosition.Left, true);
        }

    }
}

