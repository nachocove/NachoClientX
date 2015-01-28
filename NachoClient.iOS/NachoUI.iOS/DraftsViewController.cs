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
    public partial class DraftsViewController : NcUIViewControllerNoLeaks, INachoCalendarItemEditorParent
	{
        UITableView draftsTableView;
        DraftsTableViewSource draftsTableViewSource;
        DraftsHelper.DraftType draftType;

        public DraftsViewController (IntPtr handle) : base (handle)
        {

        }

        public void SetDraftType (DraftsHelper.DraftType draftType)
        {
            this.draftType = draftType;
        }

        protected override void CreateViewHierarchy ()
        {
            draftsTableViewSource = new DraftsTableViewSource ();
            draftsTableViewSource.SetOwner (this);

            switch (draftType) {
            case DraftsHelper.DraftType.Calendar:
                draftsTableViewSource.SetDraftType (DraftsHelper.DraftType.Calendar);
                draftsTableViewSource.SetDraftsList (DraftsHelper.GetCalendarDrafts (LoginHelpers.GetCurrentAccountId ()).Cast<McAbstrItem> ().ToList ());
                break;
            }

            draftsTableView = new UITableView (View.Frame);
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

        }

        public void DraftItemSelected (DraftsHelper.DraftType draftType, McAbstrItem draft)
        {
            switch (draftType) {
            case DraftsHelper.DraftType.Calendar:
                PerformSegue ("SegueToEditEvent", new SegueHolder ((McCalendar)draft));
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

        protected void LoadDrafts ()
        {
            switch (draftType) {
            case DraftsHelper.DraftType.Calendar:
                draftsTableViewSource.SetDraftsList (DraftsHelper.GetCalendarDrafts (LoginHelpers.GetCurrentAccountId ()).Cast<McAbstrItem> ().ToList ());
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

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }
	}
}
