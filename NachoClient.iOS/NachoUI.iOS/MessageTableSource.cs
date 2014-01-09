using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class MessageTableSource  : UITableViewSource
    {
        protected McFolder currentFolder;

        AppDelegate appDelegate { get; set; }

        public MessageTableSource (McFolder ncfolder)
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
            currentFolder = ncfolder;
        }

        public override int RowsInSection (UITableView tableview, int section)
        {
            return BackEnd.Instance.Db.Table<McEmailMessage> ().Where (rec => rec.FolderId == this.currentFolder.Id).Count ();
        }

        public override UITableViewCell GetCell (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
        {
            UITableViewCell cell = tableView.DequeueReusableCell ("msgheader");

            var msgHeader = BackEnd.Instance.Db.Table<McEmailMessage> ().OrderByDescending (rec => rec.DateReceived).Where (rec => rec.FolderId == this.currentFolder.Id).ElementAt (indexPath.Row);
            cell.TextLabel.Text = msgHeader.Subject;
            cell.DetailTextLabel.Text = msgHeader.From;
        
            return cell;
        }

        public McEmailMessage getEmailMessage (NSIndexPath id)
        {
            // force this to happen. Might be smarter to just pass the index, then, since the appDelegate
            // is common for all objects, the indexID in the selected row should be the NcFolder (or other type)
            // had to fox to make sure that all references are in sunc. see "GetCell" above
            return BackEnd.Instance.Db.Table<McEmailMessage> ().OrderByDescending (rec => rec.DateReceived).Where (rec => rec.FolderId == this.currentFolder.Id).ElementAt (id.Row);
           
        }
    }
}

