using System;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using MonoTouch.Foundation;


namespace NachoClient.iOS
{
    public class AccountTableSource : UITableViewSource
    {
        public AccountTableSource ()
        {

        }
        public override int RowsInSection (UITableView tableview, int section)
        {
            // NOTE: Don't call the base implementation on a Model class
            // see http://docs.xamarin.com/ios/tutorials/Events%2c_Protocols_and_Delegates 
            return NcModel.Instance.Db.Table<McAccount> ().Count ();
        }
        public override UITableViewCell GetCell (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
        {
            // NOTE: Don't call the base implementation on a Model class
            // see http://docs.xamarin.com/ios/tutorials/Events%2c_Protocols_and_Delegates 
            UITableViewCell cell = tableView.DequeueReusableCell ("dorkknob");
            var account= NcModel.Instance.Db.Table<McAccount> ().ElementAt (indexPath.Row);
            cell.TextLabel.Text = account.EmailAddr;
            return cell;
        }
        public McAccount GetAccount(NSIndexPath id) {
            return NcModel.Instance.Db.Table<McAccount>().ElementAt (id.Row);
        }
        /*public int curAccountID() {

            var account= BackEnd.Instance.Db.Table<NcAccount> ().ElementAt(indexPath.Row);
            Console.Write(" Row number = ");
            Console.WriteLine (RowSelected);

            return account.AccountId;

        }*/

    }
}

