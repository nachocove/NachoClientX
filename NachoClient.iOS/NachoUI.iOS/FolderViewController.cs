// This file has been autogenerated from a class added in the UI designer.

using System;
using MonoTouch.Foundation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MonoTouch.UIKit;
using SWRevealViewControllerBinding;
using System.Drawing;

namespace NachoClient.iOS
{
    public partial class FolderViewController : NcUITableViewController, IUITableViewDelegate
    {
        McAccount currentAccount { get; set; }

        public void SetAccount (McAccount ncaccount)
        {
            currentAccount = ncaccount;
        }

        HierarchicalFolderTableSource folders;

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            var addButton = new UIBarButtonItem (UIBarButtonSystemItem.Add);
            addButton.TintColor = A.Color_NachoBlue;
            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] { addButton };

            // Stylize TableView
            folders = new HierarchicalFolderTableSource (TableView);
            TableView.DataSource = folders;
            TableView.SeparatorColor = A.Color_NachoBorderGray;
            UISearchBar sb = new UISearchBar (new RectangleF (0, 45, TableView.Frame.Width, 45));
            sb.BarTintColor = A.Color_NachoLightGrayBackground;
            sb.Placeholder = "Search";
            NSString x = new NSString ("_searchField");
            UITextField txtField = (UITextField)sb.ValueForKey (x);
            txtField.BackgroundColor = UIColor.White;
            TableView.TableHeaderView = sb;

            // Initially let's hide the search controller
            TableView.SetContentOffset (new PointF (0.0f, 44.0f), false);

            // Watch for changes from the back end
            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_FolderSetChanged == s.Status.SubKind) {
                    folders.Refresh ();
                    this.TableView.ReloadData ();
                }
            };
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            folders.Refresh ();
            this.TableView.ReloadData ();
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "FolderToMessageList") {
                var msgview = (MessageListViewController)segue.DestinationViewController; //our destination
                var source = TableView.DataSource as HierarchicalFolderTableSource;
                var rowPath = TableView.IndexPathForSelectedRow;
                var folder = source.getFolder (rowPath);
                var messageList = new NachoEmailMessages (folder);
                msgview.SetEmailMessages (messageList);
                return;
            }

            if (segue.Identifier.Equals ("SegueToNachoNow")) {
                // Nothing to do
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();

        }

        public FolderViewController (IntPtr handle) : base (handle)
        {
        }
    }
}
