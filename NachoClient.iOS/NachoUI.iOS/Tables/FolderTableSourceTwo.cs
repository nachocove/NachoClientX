using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using NachoCore;
using NachoCore.Model;
using System.Collections;

namespace NachoClient.iOS
{
    public class FolderTableSourceTwo  : UITableViewDataSource
    {
        NachoFolders folders;
        public List<McFolder> foldersToMcFolders = new List<McFolder> ();
        public List<McFolder> searchResults = null;
        public List<FolderStruct> nestedFolderList = new List<FolderStruct> ();
        public List<FlattenedFolderStruct> flattenedFolderList = new List<FlattenedFolderStruct> ();
        public List<UITableViewCell> FolderCells = new List<UITableViewCell> ();
        public UITableView foldersTable;
        public string whatType;

        public FolderTableSourceTwo (ref UITableView table)
        {
            foldersTable = table;
            folders = new NachoFolders (NachoFolders.FilterForEmail);
            convertFoldersToMcFolders ();
            createNestedFolderList ();
            flattenNestedFolderList (0, nestedFolderList);
        }

        public FolderTableSourceTwo(string whatType, UITableView theTable)
        {
            folders = new NachoFolders (NachoFolders.FilterForEmail);

            //whatType: The FolderTableSource is created by 2 different view controllers, MessageActionViewController and FolderViewController
            //          whatType can either be "folderSegue" or "folderAction" 
            //          "folderSegue" is the FolderViewController version, this is the view from the Folders option on the main menu where the user select a folder and it brings
            //          inside that directory so they can view the messages contained by it
            //          "folderAction" is the MessageActionViewController version, this is the view accessed when viewing a single message, and they have the ability to file the 
            //          message in a specific directoryt
            this.whatType = whatType;
            foldersTable = theTable;
            convertFoldersToMcFolders ();
            createNestedFolderList ();
            flattenNestedFolderList (0, nestedFolderList);
            CreateCells ();
        }

        public void convertFoldersToMcFolders()
        {
            for (int i = 0; i < folders.Count (); i++) {
                foldersToMcFolders.Add (folders.GetFolder (i));
            }
        }

        public override int RowsInSection (UITableView tableview, int section)
        {
            return foldersToMcFolders.Count;
        }

        public void createNestedFolderList ()
        {
            foreach (var folder in foldersToMcFolders) {
            
                if (Int32.Parse (folder.ParentId) == 0) {
                    int folderID = folder.Id;
                    string fname = folder.DisplayName;
                    List<FolderStruct> subFolders = new List<FolderStruct> ();
                    subFolders = getSubFolders (folder.Id, folder.AccountId, folder.ServerId, 0);
                    nestedFolderList.Add (new FolderStruct (folderID, subFolders, fname));
                }
            }
        }

        public List<FolderStruct> getSubFolders (int fID, int accountID, string serverID, int indentLevel)
        {
            indentLevel += 1;
            List<FolderStruct> subFolders = new List<FolderStruct> ();
            List<McFolder> folds = new List<McFolder> ();
            folds = McFolder.QueryByParentId (accountID, serverID);

            foreach (McFolder f in folds) {
                subFolders.Add (new FolderStruct (f.Id, getSubFolders (f.Id, f.AccountId, f.ServerId, indentLevel), f.DisplayName));
            }
            return subFolders;
        }

        public void flattenNestedFolderList (int indentlevel, List<FolderStruct> nestedFoldersList)
        {
            foreach (FolderStruct f in nestedFoldersList) {

                flattenedFolderList.Add (new FlattenedFolderStruct (f.folderName, f.folderID, indentlevel));

                if (f.subFolders.Count != 0) {
                    flattenNestedFolderList (indentlevel + 1, f.subFolders);
                }
            }
        }

        public class FolderStruct
        {
            public int folderID { get; set; }
            public string folderName { get; set; }
            public List <FolderStruct> subFolders { get; set; }

            public FolderStruct ()
            {

            }

            public FolderStruct (int fid, List<FolderStruct> sf, string fn)
            {
                folderID = fid;
                subFolders = sf;
                folderName = fn;
            }
        }

        public class FlattenedFolderStruct
        {
            public string folderName { get; set; }
            public int folderID { get; set; }
            public int indentLevel { get; set; }

            public FlattenedFolderStruct (string fn, int fid, int il)
            {
                folderName = fn;
                folderID = fid;
                indentLevel = il;
            }
        }

        public void CreateCells()
        {
            foreach (var theFolder in flattenedFolderList) {
                CreateCell (theFolder);
            }
        }

        public void CreateCell(FlattenedFolderStruct theFolder)
        {
            UITableViewCell theCell;
            bool isArchiveEmail;
            string initialSpacing = "";
            UIView cellIconSpace = new UIView ();
            bool isRootFolder = theFolder.indentLevel == 0 ? true : false;

            if (string.Equals (whatType, "folderSegue", StringComparison.Ordinal)) {
                isArchiveEmail = false;
            } else {
                isArchiveEmail = true;
            }

            RectangleF cellFrame = new RectangleF (foldersTable.Frame.X - 20.0f, foldersTable.Frame.Y, foldersTable.Frame.Width, foldersTable.Frame.Height);

            if (isArchiveEmail) {
                theCell = new UITableViewCell (cellFrame);
                theCell.SeparatorInset = new UIEdgeInsets (0, 0, 0, 0);
            } else {
                theCell = foldersTable.DequeueReusableCell ("mailview");
                theCell.SeparatorInset = new UIEdgeInsets (0, 15, 0, 0);
            }

            theCell.TextLabel.Font = A.Font_AvenirNextRegular14;
            theCell.TextLabel.TextColor = A.Color_999999;
            theCell.BackgroundColor = UIColor.White;
            theCell.ContentMode = UIViewContentMode.Left;

            if (isRootFolder) {
                theCell.ImageView.Image = UIImage.FromBundle ("icn-folder");
                theCell.TextLabel.Font = A.Font_AvenirNextDemiBold14;
            } else {
                initialSpacing = "                 " + "".PadRight (theFolder.indentLevel * 5);
                cellIconSpace = new UIView (new RectangleF (10 * theFolder.indentLevel, 4, 55, 26));
                UIImageView iconOne = new UIImageView (new RectangleF (cellIconSpace.Frame.X, cellIconSpace.Frame.Y, cellIconSpace.Frame.Width, cellIconSpace.Frame.Height));
                iconOne.Image = UIImage.FromBundle ("icn-arrow-folder2");
                cellIconSpace.AddSubview (iconOne);
                theCell.ContentView.AddSubview(cellIconSpace);
            }

            theCell.TextLabel.Text = initialSpacing + theFolder.folderName;
            theCell.Accessory = UITableViewCellAccessory.None;

            FolderCells.Add (theCell);
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            return FolderCells [indexPath.Row];
        }

        public McFolder getFolder (NSIndexPath indexPath)
        {
            return folders.GetFolderByFolderID (flattenedFolderList [indexPath.Row].folderID);
        }

        public void Refresh ()
        {
            folders.Refresh ();
        }

        public bool UpdateSearchResults (string forSearchString)
        {
            var account = NcModel.Instance.Db.Table<McAccount> ().First ();
            var searchResults = McFolder.SearchFolders (account.Id, forSearchString);
            foldersToMcFolders = searchResults;
            foldersTable.ReloadData ();
            return true;

            //update folders
            //refresh
            //updatetable
        }
    }

    public class SearchDisplayDelegate : UISearchDisplayDelegate
    {
        FolderTableSourceTwo v;

        private SearchDisplayDelegate ()
        {
        }

        public SearchDisplayDelegate (FolderTableSourceTwo owner)
        {
            v = owner;
        }

        public override bool ShouldReloadForSearchScope (UISearchDisplayController controller, int forSearchOption)
        {
            // TODO: Trigger asynch search & return false
            string searchString = controller.SearchBar.Text;
            return v.UpdateSearchResults (searchString);
        }

        public override bool ShouldReloadForSearchString (UISearchDisplayController controller, string forSearchString)
        {
            // TODO: Trigger asynch search & return false
            int searchOption = controller.SearchBar.SelectedScopeButtonIndex;
            return v.UpdateSearchResults (forSearchString);
        }
    }
}



