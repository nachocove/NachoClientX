using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class FolderTableSource  : UITableViewSource
    {
        AppDelegate appDelegate { get; set; }

        public FolderTableSource ()
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
        }
        public override int RowsInSection (UITableView tableview, int section)
        {
            // NOTE: Don't call the base implementation on a Model class
            // see http://docs.xamarin.com/ios/tutorials/Events%2c_Protocols_and_Delegates 
            // get somethinng here

            // FIX: need association with account ID
            return appDelegate.Be.Db.Table<NcFolder> ().Count ();
        }
        public override UITableViewCell GetCell (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
        {
            // NOTE: Don't call the base implementation on a Model class
            // see http://docs.xamarin.com/ios/tutorials/Events%2c_Protocols_and_Delegates 
            UITableViewCell cell = tableView.DequeueReusableCell ("mailview");
            var folder= appDelegate.Be.Db.Table<NcFolder> ().ElementAt (indexPath.Row);
            // so at each "indexPathRow" in the array (think of it that way; we have a NcFoldertype);
            cell.TextLabel.Text = folder.DisplayName;

            return cell;
        }
        public NcFolder getFolder (NSIndexPath id){
            // force this to happen. Might be smarter to just pass the index, then, since the appDelegate
            // is common for all objects, the indexID in the selected row should be the NcFolder (or other type)..
            return appDelegate.Be.Db.Table<NcFolder> ().ElementAt (id.Row);
        }

    }
}

