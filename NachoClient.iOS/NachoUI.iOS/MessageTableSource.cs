using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{

	public class MessageTableSource  : UITableViewSource
	{
		protected NcFolder currentFolder;

		AppDelegate appDelegate { get; set; }

		public MessageTableSource (NcFolder ncfolder)
		{
			appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
			currentFolder = ncfolder;
		}
		public override int RowsInSection (UITableView tableview, int section)
		{
			int sillybug;
			//Need to get the number of rows in THIS folder; may need to move MessageTableSource
			// to be included in MessageviewController

			// FIXME : Need to associate with Account.Id and Current_folder (folder tapped)
			// FIXME : when a new email comes in, this aray needs to be reconstituted, otherwise an error occurs

			//return appDelegate.Be.Db.Table<NcEmailMessage> ().Where (rec => rec.FolderId == this.currentFolder).Count();
			sillybug = appDelegate.Be.Db.Table<NcEmailMessage> ().Where (rec => rec.FolderId == this.currentFolder.Id).Count();
			Console.Write ("number of rows in this folder = ");
			Console.WriteLine (sillybug);
			return appDelegate.Be.Db.Table<NcEmailMessage> ().Where (rec => rec.FolderId == this.currentFolder.Id).Count();

		}
		public override UITableViewCell GetCell (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
		{
			 

			UITableViewCell cell = tableView.DequeueReusableCell ("msgheader");
			//var accountdetail = appDelegate.Be.Db.Tabe<NcAccount> ().ElementAt (self.AccountIndexinfo);


			var msgHeader= appDelegate.Be.Db.Table<NcEmailMessage>() .Where (rec=> rec.FolderId ==this.currentFolder.Id).ElementAt (indexPath.Row);
			cell.TextLabel.Text = msgHeader.Subject;
			cell.DetailTextLabel.Text = msgHeader.From;
			//Console.WriteLine (msgHeader.Body);
		
			return cell;
		}
		public NcEmailMessage getEmailMessage (NSIndexPath id){
			// force this to happen. Might be smarter to just pass the index, then, since the appDelegate
			// is common for all objects, the indexID in the selected row should be the NcFolder (or other type)..
			return appDelegate.Be.Db.Table<NcEmailMessage> ().ElementAt (id.Row);
		}

	}
}

