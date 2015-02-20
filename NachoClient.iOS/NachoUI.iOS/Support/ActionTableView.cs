//using System;
//using System.Drawing;
//using System.Collections.Generic;
//using MonoTouch.Foundation;
//using MonoTouch.UIKit;
//using UIImageEffectsBinding;
//using MonoTouch.CoreGraphics;
//
//namespace NachoClient.iOS
//{
//    [Register ("ActionTableView")]
//    public class ActionTableView : NcUITableViewController
//    {
//        UITableView folderTableView { get; set; }
//        UITableViewDataSource foldersDataSource;
//        INachoFolderChooser folders;
//        ActionView owner;
//
//        public ActionTableView (){
//
//        }
//
//        public ActionTableView (RectangleF frame, UITableViewSource dataSource)
//        {
//            foldersDataSource = dataSource;
//        }
//
//
//        public ActionTableView (IntPtr handle) : base (handle)
//        {
//
//        }
//
//        protected class FolderTableDelegate : UITableViewDelegate
//        {
//            MessageActionViewController owner = null;
//
//            public FolderTableDelegate (MessageActionViewController owner)
//            {
//                this.owner = owner;
//            }
//
//            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
//            {
//                var folderSource = (FolderTableSource)tableView.DataSource;
//                var folder = folderSource.getFolder (indexPath);
//                owner.FolderSelected (folder);
//            }
//        }
//
//        public void SetOwner (ActionView owner)
//        {
//            this.owner = owner;
//        }
//
//        public override void ViewDidLoad ()
//        {
//            base.ViewDidLoad ();
//            folderTableView.Delegate = new FolderTableDelegate (this);
//
//
//
//        }
//
//        public override void ViewWillAppear (bool animated)
//        {
//            base.ViewWillAppear (animated);
//            var folderSource = new FolderTableSource ();
//            folderTableView.DataSource = folderSource;
//        }
//    }
//}