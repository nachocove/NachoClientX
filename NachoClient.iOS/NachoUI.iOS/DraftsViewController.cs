using System;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using NachoCore.Model;
using NachoCore.Utils;
using System.Collections.Generic;
using System.Linq;
using NachoCore.ActiveSync;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class DraftsViewController : NcUIViewControllerNoLeaks, INachoCalendarItemEditorParent, INachoMessageEditorParent
    {
        UITableView draftsTableView;
        DraftsTableViewSource draftsTableViewSource;
        DraftsHelper.DraftType draftType;

        UIBarButtonItem composeButton;

        public DraftsViewController (IntPtr handle) : base (handle)
        {

        }

        public void SetDraftType (DraftsHelper.DraftType draftType)
        {
            this.draftType = draftType;
        }

        protected override void CreateViewHierarchy ()
        {
            View.BackgroundColor = A.Color_NachoBackgroundGray;
            draftsTableViewSource = new DraftsTableViewSource ();
            draftsTableViewSource.SetOwner (this);

            composeButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (composeButton, "contact-newemail");
            composeButton.Clicked += ComposeButtonClicked;
            NavigationItem.RightBarButtonItem = composeButton;

            switch (draftType) {
//            case DraftsHelper.DraftType.Calendar:
//                draftsTableViewSource.SetDraftType (DraftsHelper.DraftType.Calendar);
//                draftsTableViewSource.SetDraftsList (DraftsHelper.GetCalendarDrafts (LoginHelpers.GetCurrentAccountId ()).Cast<McAbstrItem> ().ToList ());
//                break;
            case DraftsHelper.DraftType.Email:
                draftsTableViewSource.SetDraftType (DraftsHelper.DraftType.Email);
                break;
            }
           
            draftsTableView = new UITableView (new RectangleF (0, 0, View.Frame.Width, View.Frame.Height - 100));
            draftsTableView.SeparatorColor = A.Color_NachoLightBorderGray;
            draftsTableView.BackgroundColor = A.Color_NachoLightBorderGray;
            draftsTableView.Source = draftsTableViewSource;
            View.AddSubview (draftsTableView);
        }

        protected override void ConfigureAndLayout ()
        {
            Util.ConfigureNavBar (false, this.NavigationController);
            LoadDrafts ();

            switch (draftType) {
            case DraftsHelper.DraftType.Calendar:
                NavigationItem.Title = "Calendar Drafts";
                break;
            case DraftsHelper.DraftType.Email:
                NavigationItem.Title = "Email Drafts";
                break;
            }
        }

        protected override void Cleanup ()
        {
            composeButton.Clicked -= ComposeButtonClicked;
            composeButton = null;
        }

        protected void ComposeButtonClicked (object sender, EventArgs e)
        {
            PerformSegue ("SegueToMessageCompose", new SegueHolder (null, false));
        }

        public void DraftItemSelected (DraftsHelper.DraftType draftType, McAbstrItem draft)
        {
            switch (draftType) {
            case DraftsHelper.DraftType.Calendar:
                PerformSegue ("SegueToEditEvent", new SegueHolder ((McCalendar)draft));
                break;
            case DraftsHelper.DraftType.Email:
                PerformSegue ("SegueToMessageCompose", new SegueHolder ((McEmailMessage)draft, true));
                break;
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

        public void LoadDrafts ()
        {
            switch (draftType) {
//            case DraftsHelper.DraftType.Calendar:
//                draftsTableViewSource.SetDraftsList (DraftsHelper.GetCalendarDrafts (LoginHelpers.GetCurrentAccountId ()).Cast<McAbstrItem> ().ToList ());
//                draftsTableView.ReloadData ();
//                break;
            case DraftsHelper.DraftType.Email:
                draftsTableViewSource.SetDraftsList (DraftsHelper.GetEmailDrafts (LoginHelpers.GetCurrentAccountId ()).Cast<McAbstrItem> ().ToList ());
                draftsTableView.ReloadData ();
                break;
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "SegueToEditEvent") {
                var vc = (EditEventViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var calendar = holder.value as McCalendar;
                vc.SetCalendarItem (calendar);
                vc.SetOwner (this);
                return;
            }

            if (segue.Identifier.Equals ("SegueToMessageCompose")) {
                var h = sender as SegueHolder;
                //h.value2 == isDraft
                if ((bool)h.value2 == true) {
                    McEmailMessage draftMessage = (McEmailMessage)h.value;
                    McEmailMessageThread tempThread = new McEmailMessageThread ();
                    NcEmailMessageIndex tempIndex = new NcEmailMessageIndex ();
                    tempIndex.Id = draftMessage.Id;
                    tempThread.Add (tempIndex);
                    INachoMessageComposer mcvc = (INachoMessageComposer)segue.DestinationViewController;
                    mcvc.SetDraftPresetFields (EmailHelper.EmailMessageRecipients (draftMessage), draftMessage.Subject, draftMessage.GetBodyString (), McAttachment.QueryByItemId(draftMessage));
                    mcvc.SetAction (tempThread, MessageComposeViewController.EDIT_DRAFT_ACTION);
                    return;
                } else {
                    var vc = (INachoMessageComposer)segue.DestinationViewController;
                    vc.SetAction (null, null);
                    return;
                }
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        // INachoMessageEditorParent
        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            NcAssert.CaseError ();
        }

        // INachoMessageEditorParent
        public void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            NcAssert.CaseError ();
        }

        // INachoMessageEditorParent
        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, null);
        }
    }
}
