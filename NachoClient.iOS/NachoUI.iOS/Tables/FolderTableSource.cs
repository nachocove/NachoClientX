using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class FolderTableSource  : UITableViewDataSource
    {
        NachoFolders folders;

        public UIColor cellTextColor = UIColor.Black;

        public FolderTableSource ()
        {
            folders = new NachoFolders (NachoFolders.FilterForEmail);
        }

        public override int RowsInSection (UITableView tableview, int section)
        {
            return folders.Count ();
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = tableView.DequeueReusableCell ("mailview");
            var folder = folders.GetFolder (indexPath.Row);
            cell.TextLabel.Text = folder.DisplayName;
            cell.TextLabel.TextColor = cellTextColor;
            cell.BackgroundColor = UIColor.Clear;
            return cell;
        }

        public McFolder getFolder (NSIndexPath indexPath)
        {
            return folders.GetFolder (indexPath.Row);
        }

        public void Refresh()
        {
            folders.Refresh ();
        }
    }
}

