// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using System.Collections.Generic;
using NachoCore;
using NachoCore.Model;

using MonoTouch.Foundation;
using MonoTouch.UIKit;



namespace NachoClient.iOS
{
    
    public partial class AccountViewController : UITableViewController
    {

        public override void ViewDidLoad ()
        {


            base.ViewDidLoad ();
            TableView.Source = new AccountTableSource ();

            Console.WriteLine ("Account Tables shown");
        

        }
        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            // prepare for segue. Target view controller here is folderView
            // pass the account ID to the target view controller, so that we show the right
            // folders here.
            NcAccount thisaccount;

            Console.WriteLine ("In accountviewController PrepareforSegue");

            if (segue.Identifier == "dorkknob") {
                var fldrview = (FolderViewController)segue.DestinationViewController; //our destination
                if (fldrview != null) {
                    Console.Write (" Calling Folder View in dorkknob segue");
                    //fldrview.curAccountId = TableView.IndexPathForSelectedRow;
                    var source = TableView.Source as AccountTableSource;
                    var rowPath = TableView.IndexPathForSelectedRow;
                    thisaccount = source.GetAccount (rowPath);
                    //var ncaccount = source.GetItem (rowPath);
                    fldrview.SetAccount (thisaccount);
                }

            }
        }
    




        public AccountViewController (IntPtr handle) : base (handle)
        {
            //TableView.Source = new AccountTableSource ();

                // Custom initialization
        }

    }

}
