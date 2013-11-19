// This file has been autogenerated from a class added in the UI designer.

using System;
using NachoCore;
using NachoCore.Model;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MimeKit.Utils;
using MimeKit;



namespace NachoClient.iOS
{
    public partial class MessageViewController : UITableViewController
    {
        NcFolder currentFolder { get; set; }
        public void SetFolder (NcFolder ncfolder) {
            currentFolder = ncfolder;
        }
        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            NcEmailMessage thisemailmsg;

            if (segue.Identifier == "readmessagesegue" ){
                var rdmsg = (ReadMsgController)segue.DestinationViewController; //our destination

                var source = TableView.Source as MessageTableSource;
                var rowPath = TableView.IndexPathForSelectedRow;
                thisemailmsg = source.getEmailMessage(rowPath);
                rdmsg.SetMessage(thisemailmsg);

                //Console.Write ("The index of this row is ");
                //Console.WriteLine(rowPath);

                // needt to find the "index of this current cell; then pass the FolderID and AccountID in the
                // segue to the MessageViewController, so we show only messages from this folder.
                /*FIX
            msgview.currentfolder = (NcFolder)(thisview.selectedindexrow)
            msgview.currentaccount = appDe
*/
}
        }




        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            TableView.Source = new MessageTableSource (currentFolder);
        }


        public MessageViewController (IntPtr handle) : base (handle)
        {
        }
    }
}
