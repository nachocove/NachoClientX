//using System;
//using System.Drawing;
//using System.Collections.Generic;
//using MonoTouch.Foundation;
using MonoTouch.UIKit;
using UIImageEffectsBinding;
using MonoTouch.CoreGraphics;

using System;
using System.Collections.Generic;
using MonoTouch.Foundation;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    [Register ("ActionTableView")]
    public class ActionTableView : UITableView, IUITableViewDelegate, INachoFolderChooser
    {
        protected object cookie;
        protected INachoFolderChooserParent owner;
        protected INachoFolders folders = null;
        protected FolderTableDelegate folderTableDelegate;

        public ActionTableView()
        {

        }
        public ActionTableView (IntPtr handle) : base (handle)
        {
        }

        public void SetOwner (INachoFolderChooserParent owner, object cookie)
        {
            this.owner = owner;
            this.cookie = cookie;
        }

        public void DismissFolderChooser (bool animated, NSAction action)
        {
            owner = null;
            cookie = null;
        }

        public void initTable()
        {
            folderTableDelegate = new FolderTableDelegate (this);
            this.BackgroundColor = UIColor.Blue;
            var folderSource = new FolderTableSource ();
            folderSource.cellTextColor = UIColor.Black;
            this.DataSource = folderSource;
        }

        public void FolderSelected (McFolder folder)
        {
            owner.FolderSelected (this, folder, cookie);
        }

        protected class FolderTableDelegate : UITableViewDelegate
        {
            ActionTableView owner = null;

            public FolderTableDelegate (ActionTableView owner)
            {
                this.owner = owner;
            }

            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
                var folderSource = (FolderTableSource)tableView.DataSource;
                var folder = folderSource.getFolder (indexPath);
                owner.FolderSelected (folder);
            }
        }
    }
}