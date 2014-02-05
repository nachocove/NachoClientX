// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public partial class MessageActionViewController : BlurryViewController, IUITableViewDelegate, INachoMessageController
    {
        public List<McEmailMessage> thread;
        protected INachoMessageControllerDelegate owner;
        protected INachoFolders folders = null;

        public MessageActionViewController (IntPtr handle) : base (handle)
        {
        }

        public void SetOwner(INachoMessageControllerDelegate o)
        {
            owner = o;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            dismissButton.TouchUpInside += (object sender, EventArgs e) => {
                DismissViewController (true, null);
            };

            folderTableView.Delegate = new FolderTableDelegate (this);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            var folderSource = new FolderTableSource ();
            folderSource.cellTextColor = UIColor.White;
            folderTableView.DataSource = folderSource;
        }

        protected class FolderTableDelegate : UITableViewDelegate
        {
            MessageActionViewController owner = null;

            public FolderTableDelegate(MessageActionViewController o)
            {
                owner = o;
            }

            public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
            {
                var folderSource = (FolderTableSource)tableView.DataSource;
                var folder = folderSource.getFolder (indexPath);

                foreach (var message in owner.thread) {
                    BackEnd.Instance.MoveItemCmd (message.AccountId, message.Id, folder.Id);
                }
            }
        }
    }
}
