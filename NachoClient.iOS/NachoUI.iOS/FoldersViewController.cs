// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.EventKit;
using System.IO;
using System.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;


namespace NachoClient.iOS
{
    public partial class FoldersViewController : UIViewController
    {
        public FoldersViewController (IntPtr handle) : base (handle)
        {
        }

        protected McAccount account;
        protected bool hasRecents = false;
        protected bool isFirstConfigure = true;
        protected UILabel recentLabel;
        protected UILabel defaultsLabel;
        protected UILabel yourFoldersLabel;
        protected UIView recentView;
        protected UIView defaultsView;
        protected UIView yourFoldersView;
        protected UIScrollView scrollView;
        protected int rootFolderCount;

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
            CreateView ();
            ConfigureFolders ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            ConfigureView ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            foreach (var v in defaultsView.Subviews) {
                v.RemoveFromSuperview ();
            }

            foreach (var v in yourFoldersView.Subviews) {
                v.RemoveFromSuperview ();
            }
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "FoldersToMessageList") {
                var holder = (SegueHolder)sender;
                var messageList = new NachoEmailMessages ((McFolder)holder.value);
                var messageListViewController = (MessageListViewController)segue.DestinationViewController;
                messageListViewController.SetEmailMessages (messageList);
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        protected void CreateView ()
        {
            NavigationItem.Title = "Folders";
            scrollView = new UIScrollView (new RectangleF (0, 0, View.Frame.Width, View.Frame.Height));

            float yOffset = 20f;

            float marginPadding = 15f;

            recentLabel = new UILabel (new RectangleF (20, yOffset, 160, 20));
            recentLabel.Text = "Recent Folders";
            recentLabel.Font = A.Font_AvenirNextDemiBold17;
            recentLabel.TextColor = A.Color_NachoGreen;
            recentLabel.Hidden = true;
            yOffset += recentLabel.Frame.Height;

            yOffset += 15;
            recentView = new UIView (new RectangleF (marginPadding / 2, yOffset, View.Frame.Width - marginPadding, 44));
            recentView.Layer.CornerRadius = 4;
            recentView.BackgroundColor = UIColor.White;
            recentView.Hidden = true;
            yOffset += recentView.Frame.Height;

            yOffset += 20;
            defaultsLabel = new UILabel (new RectangleF (20, yOffset, 160, 20));
            defaultsLabel.Text = "Default Folders";
            defaultsLabel.Font = A.Font_AvenirNextDemiBold17;
            defaultsLabel.TextColor = A.Color_NachoGreen;
            yOffset += defaultsLabel.Frame.Height;

            yOffset += 5;
            defaultsView = new UIView (new RectangleF (marginPadding / 2, yOffset, View.Frame.Width - marginPadding, 44));
            defaultsView.Layer.CornerRadius = 4;
            defaultsView.BackgroundColor = UIColor.White;
            yOffset += defaultsView.Frame.Height;

            yOffset += 20;
            yourFoldersLabel = new UILabel (new RectangleF (20, yOffset, 160, 20));
            yourFoldersLabel.Text = "Your Folders";
            yourFoldersLabel.Font = A.Font_AvenirNextDemiBold17;
            yourFoldersLabel.TextColor = A.Color_NachoGreen;
            yourFoldersLabel.Hidden = true;
            yOffset += yourFoldersLabel.Frame.Height;

            yOffset += 5;
            yourFoldersView = new UIView (new RectangleF (marginPadding / 2, yOffset, View.Frame.Width - marginPadding, 44));
            yourFoldersView.Layer.CornerRadius = 4;
            yourFoldersView.BackgroundColor = UIColor.White;
            yourFoldersView.Hidden = true;
            yOffset += yourFoldersView.Frame.Height;

            yOffset += 20;
            scrollView.Add (recentLabel);
            scrollView.Add (recentView);

            scrollView.Add (defaultsLabel);
            scrollView.Add (defaultsView);
            scrollView.Add (yourFoldersLabel);
            scrollView.Add (yourFoldersView);
            scrollView.BackgroundColor = A.Color_NachoBackgroundGray;
            scrollView.ContentSize = new SizeF (View.Frame.Width, yOffset);
            View.Add (scrollView);
        }

        protected void ConfigureView ()
        {
            UpdateLastAccessed ();
            if (0 != recentFolderList.Count) {
                recentLabel.Hidden = false;
                recentView.Hidden = false;
                hasRecents = true;
            } 
            var index = 0;
            foreach (var folder in recentFolderList) {
                CreateFolderCell (0, recentView, index, index, folder.DisplayName, false, false, 2000 + index);
                index++;
            }
            recentView.Frame = new RectangleF (recentView.Frame.X, recentView.Frame.Y, recentView.Frame.Width, 3 * 44);

            index = 0;
            foreach (var f in flattenedFolderList) {
                var isRootFolder = true;
                if (f.indentLevel == 0) {
                    rootFolderCount++;
                    isRootFolder = false;
                }
                CreateFolderCell (f.indentLevel, defaultsView, rootFolderCount - 1, index, f.folderName, HasSubFolders (index), isRootFolder, 2000 + index);
                index++;
            }

            yourFoldersLabel.Hidden = true;
            yourFoldersView.Hidden = true;

            index = 0;
            foreach (var f in flattenedFolderList) {
                var isRootFolder = true;
                if (f.indentLevel == 0) {
                    rootFolderCount++;
                    isRootFolder = false;
                }
                CreateFolderCell (f.indentLevel, yourFoldersView, rootFolderCount - 1, index, f.folderName, HasSubFolders (index), isRootFolder, 2000 + index);
                index++;
            }
            if (isFirstConfigure) {
                UpdateVisibleCellsList (defaultsView, "DefaultFolders", flattenedFolderList.Count);
                isFirstConfigure = false;
            }
            LayoutView ();
        }

        protected void LayoutView ()
        {
            var visibleDefaultFoldersCells = McMutables.GetOrCreate (McAccount.GetDeviceAccount ().Id, "DefaultFolders", "VisibleDefaultFolders", null);
            string[] tags = null;
            if (null != visibleDefaultFoldersCells) {
                tags = visibleDefaultFoldersCells.Split (',');
            }
            var selectedDefaultButtons = McMutables.GetOrCreate (McAccount.GetDeviceAccount ().Id, "SelectedButtons", "FoldersSelectedButtons", null);
            if (null != selectedDefaultButtons) {
                string[] selectedButtons = selectedDefaultButtons.Split (',');
                UpdateVisibleCells (defaultsView, flattenedFolderList, selectedButtons);
            } else { 
                UpdateVisibleCells (defaultsView, flattenedFolderList, null);
            }

            var yOffset = 0f;
            if (hasRecents) {
                yOffset += recentView.Frame.Bottom;
            }

            yOffset += 24;
            defaultsLabel.Frame = new RectangleF (20, yOffset, 160, 20);
            yOffset += 35;
            var rootFolderIndex = 0;
            for (int i = 0; i < flattenedFolderList.Count; i++) {
                var cell = defaultsView.ViewWithTag (2000 + i) as UIView;
                if (tags.Contains (cell.Tag.ToString ())) {
                    cell.Frame = new RectangleF (cell.Frame.X, 44 * rootFolderIndex, cell.Frame.Width, 44);
                    if (cell.Tag.ToString () == tags.Last ()) {
                        cell.ViewWithTag (4000 + i).Hidden = true;
                    } else {
                        cell.ViewWithTag (4000 + i).Hidden = false;
                    }
                    rootFolderIndex++;
                } else {
                    MatchParentY (cell, i, defaultsView);
                }
            }
            var folderListHeight = rootFolderIndex * 44;
            defaultsView.Frame = new RectangleF (defaultsView.Frame.X, yOffset, defaultsView.Frame.Width, folderListHeight);
            yOffset += folderListHeight;

            //            yOffset += 20;
            //            yourFoldersLabel.Frame = new RectangleF (20, yOffset, 160, 20);
            //            yOffset += 25;
            //            rootFolderIndex = 0;
            //            for (int i = 0; i < flattenedFolderList.Count; i++) {
            //                var cell = yourFoldersView.ViewWithTag (2000 + i) as UIView;
            //                if (!cell.Hidden) {
            //                    cell.Frame = new RectangleF (cell.Frame.X, 44 * rootFolderIndex, cell.Frame.Width, 44);
            //                    if (isLastVisibleCell (i, flattenedFolderList.Count, yourFoldersView)) {
            //                        if (!isLastCell (i, flattenedFolderList.Count)) {
            //                            cell.ViewWithTag (4000 + i).Hidden = true;
            //                        }
            //                    } else {
            //                        if (!isLastCell (i, flattenedFolderList.Count)) {
            //                            cell.ViewWithTag (4000 + i).Hidden = false;
            //                        }
            //                    }
            //                    rootFolderIndex++;
            //                } else {
            //                    MatchParentY (cell, i, yourFoldersView);
            //                }
            //            }
            //            folderListHeight = rootFolderIndex * 44;
            //            yourFoldersView.Frame = new RectangleF (yourFoldersView.Frame.X, yOffset, yourFoldersView.Frame.Width, folderListHeight);
            //            yOffset += folderListHeight;

            yOffset += 45 + 64;
            scrollView.ContentSize = new SizeF (View.Frame.Width, yOffset);

        }

        public void MatchParentY (UIView cell, int index, UIView parentView)
        {
            int rootIndex = index;
            while (index >= 0) {
                if (flattenedFolderList [rootIndex].indentLevel > flattenedFolderList [index].indentLevel) {
                    cell.Frame = new RectangleF (cell.Frame.X, parentView.ViewWithTag (index + 2000).Frame.Y, cell.Frame.Width, cell.Frame.Height); 
                    cell.Hidden = true;
                    return;
                }
                index--;
            }
        }

        protected void CreateFolderCell (int subLevel, UIView parentView, int relativeIndex, int index, string title, bool subFolders, bool isHidden, int tag)
        {
            var indentation = subLevel * 10;
            UIView cell = new UIView (new RectangleF (5 + indentation, 44 * relativeIndex, parentView.Frame.Width - 10 - indentation, 44));
            cell.BackgroundColor = UIColor.White;
            var cellTap = new UITapGestureRecognizer ();
            cellTap.AddTarget (() => {
                var folder = GetFolder (index, parentView);
                folder.LastAccessed = DateTime.UtcNow;
                folder.Update ();
                UpdateLastAccessed ();
                PerformSegue ("FoldersToMessageList", new SegueHolder (folder));
            });
            cell.AddGestureRecognizer (cellTap);

            UILabel label = new UILabel (new RectangleF (52, 0, cell.Frame.Width - 52, 44));
            label.Text = title;
            label.Font = A.Font_AvenirNextMedium14;
            label.TextColor = A.Color_NachoGreen;
            cell.Add (label);

            UIImageView imageView = new UIImageView (new RectangleF (13, cell.Frame.Height / 2 - 14, 24, 24));
            imageView.Image = UIImage.FromBundle ("nav-folder");
            cell.Add (imageView);

            var count = 0;
            if (recentView == parentView) {
                count = recentFolderList.Count;
            } else if (defaultsView == parentView) {
                count = flattenedFolderList.Count;
            } else {
                count = 0;
            }

            if (!((parentView == recentView) && (isLastCell(index, count)))) {
                var line = Util.AddHorizontalLineView (52, 43, cell.Frame.Width - 47, A.Color_NachoBorderGray);
                line.Tag = tag + 2000;
                cell.Add (line);
            }

            if (subFolders) {
                UIImageView buttonImageView = new UIImageView (new RectangleF (cell.Frame.Width - 31, cell.Frame.Height / 2 - 10, 18, 18));
                buttonImageView.Image = UIImage.FromBundle ("gen-readmore");
                buttonImageView.Tag = tag + 3000;
                cell.Add (buttonImageView);

                UIButton expandButton = new UIButton (UIButtonType.RoundedRect);
                expandButton.Frame = new RectangleF (cell.Frame.Width - 40, cell.Frame.Height / 2 - 18, 36, 36);
                expandButton.TintColor = UIColor.Clear;
                expandButton.Tag = tag + 1000;
                expandButton.TouchUpInside += (sender, e) => {
                    parentView.BringSubviewToFront (cell);
                    ToggleExpandableCell (index, parentView);
                };
                cell.Add (expandButton);
            }
            cell.Hidden = isHidden;
            cell.Tag = tag;
            parentView.Add (cell);
        }

        public void ToggleExpandableCell (int index, UIView parentView)
        {
            var expandButton = parentView.ViewWithTag (3000 + index) as UIButton;
            int count = CountOfNextLevelSubFolders (index);
            int rootIndex = index;
            index++;
            if (!expandButton.Selected) {
                for (int i = 0; i < count; i++) {
                    var cell = parentView.ViewWithTag (2000 + index) as UIView;
                    if (flattenedFolderList [index].indentLevel == flattenedFolderList [rootIndex].indentLevel + 1) {
                        cell.Hidden = false;
                    }
                    index++;
                }
                expandButton.Selected = true;
                var buttonImage = parentView.ViewWithTag (5000 + rootIndex) as UIImageView;
                buttonImage.Image = UIImage.FromBundle ("gen-readmore-active");
                UpdateVisibleCellsList (parentView, "DefaultFolders", flattenedFolderList.Count);
                UpdateSelectedButtonsList (parentView, "SelectedButtons", flattenedFolderList.Count);
                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        LayoutView ();
                    },
                    () => {
                    }
                );
                return;
            } else {
                for (int i = 0; i < count; i++) {
                    var cell = parentView.ViewWithTag (2000 + index) as UIView;
                    cell.Hidden = true;
                    index++;
                }
                expandButton.Selected = false;
                var buttonImage = parentView.ViewWithTag (5000 + rootIndex) as UIImageView;
                buttonImage.Image = UIImage.FromBundle ("gen-readmore");
                UpdateVisibleCellsList (parentView, "DefaultFolders", flattenedFolderList.Count);
                UpdateSelectedButtonsList (parentView, "SelectedButtons", flattenedFolderList.Count);
                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        LayoutView ();
                    },
                    () => {
                    }
                );
                return;
            }
        }

        public void UpdateVisibleCellsList (UIView parentView, string stringName, int listCount)
        {
            string visibleCellsString = "";
            for (var i = 0; i < listCount; i++) {
                var cell = parentView.ViewWithTag (2000 + i) as UIView;
                if (false == cell.Hidden) {
                    if ("" == visibleCellsString) {
                        visibleCellsString = (2000 + i).ToString ();
                    } else {
                        visibleCellsString += "," + (2000 + i).ToString ();
                    }
                }
            }
            McMutables.Set (McAccount.GetDeviceAccount ().Id, stringName, "Visible" + stringName, visibleCellsString);
        }

        public void UpdateSelectedButtonsList (UIView parentView, string stringName, int listCount)
        {
            string selectedButtonsString = "";
            for (var i = 0; i < listCount; i++) {
                var button = parentView.ViewWithTag (3000 + i) as UIButton;
                if (null != button && true == button.Selected) {
                    if ("" == selectedButtonsString) {
                        selectedButtonsString = (3000 + i).ToString ();
                    } else {
                        selectedButtonsString += "," + (3000 + i).ToString ();
                    }
                }
            }
            McMutables.Set (McAccount.GetDeviceAccount ().Id, stringName, "Folders" + stringName, selectedButtonsString);
        }

        public void UpdateVisibleCells (UIView parentView, List<FlattenedFolderStruct> sf, string[] selectedButtons)
        {
            var cellIndex = 0;
            foreach (var cell in parentView.Subviews) {
                if (0 == sf [cellIndex].indentLevel) {
                    cell.Hidden = false;
                }
                cellIndex++;
            }
            if (null != selectedButtons) {
                foreach (var tag in selectedButtons) {
                    if ("" != tag) {
                        int numTag = Convert.ToInt32 (tag);
                        var button = parentView.ViewWithTag (numTag) as UIButton;
                        button.Selected = true;
                        var buttonImage = parentView.ViewWithTag (numTag + 2000) as UIImageView;
                        buttonImage.Image = UIImage.FromBundle ("gen-readmore-active");
                        var index = numTag % 1000;
                        int count = CountOfNextLevelSubFolders (index);
                        int rootIndex = index;
                        index++;
                        for (int i = 0; i < count; i++) {
                            var cell = parentView.ViewWithTag (2000 + index) as UIView;
                            if (sf [index].indentLevel == sf [rootIndex].indentLevel + 1) {
                                cell.Hidden = false;
                            }
                            index++;
                        }  
                    }
                }
            }
        }

        NachoFolders Folders;
        public List<McFolder> foldersToMcFolders = new List<McFolder> ();
        public List<FolderStruct> nestedFolderList = new List<FolderStruct> ();
        public List<FlattenedFolderStruct> flattenedFolderList = new List<FlattenedFolderStruct> ();
        public List<McFolder> recentFolderList = new List<McFolder> ();
        public List<UITableViewCell> FolderCells = new List<UITableViewCell> ();

        public void ConfigureFolders ()
        {
            Folders = new NachoFolders (NachoFolders.FilterForEmail);
            ConvertFoldersToMcFolders ();
            CreateNestedFolderList ();
            FlattenNestedFolderList (0, nestedFolderList);
        }

        public void ConvertFoldersToMcFolders ()
        {
            for (int i = 0; i < Folders.Count (); i++) {
                foldersToMcFolders.Add (Folders.GetFolder (i));
            }
        }

        public void CreateNestedFolderList ()
        {
            foreach (var folder in foldersToMcFolders) {
                if (Int32.Parse (folder.ParentId) == 0) {
                    int folderID = folder.Id;
                    string fname = folder.DisplayName;
                    List<FolderStruct> subFolders = new List<FolderStruct> ();
                    subFolders = GetSubFolders (folder.Id, folder.AccountId, folder.ServerId, 0);
                    nestedFolderList.Add (new FolderStruct (folderID, subFolders, fname));
                }
            }
        }

        public List<FolderStruct> GetSubFolders (int fID, int accountID, string serverID, int indentLevel)
        {
            indentLevel += 1;
            List<FolderStruct> subFolders = new List<FolderStruct> ();
            List<McFolder> folds = new List<McFolder> ();
            folds = McFolder.QueryByParentId (accountID, serverID);

            foreach (McFolder f in folds) {
                subFolders.Add (new FolderStruct (f.Id, GetSubFolders (f.Id, f.AccountId, f.ServerId, indentLevel), f.DisplayName));
            }
            return subFolders;
        }

        public void FlattenNestedFolderList (int indentlevel, List<FolderStruct> nestedFoldersList)
        {
            foreach (FolderStruct f in nestedFoldersList) {

                flattenedFolderList.Add (new FlattenedFolderStruct (f.folderName, f.folderID, indentlevel));

                if (f.subFolders.Count != 0) {
                    FlattenNestedFolderList (indentlevel + 1, f.subFolders);
                }
            }
        }

        public bool HasSubFolders (int index)
        {
            if (isLastCell (index, flattenedFolderList.Count)) {
                return false;
            }
            if (flattenedFolderList [index + 1].indentLevel > flattenedFolderList [index].indentLevel) {
                return true;
            }
            return false;

        }

        public int CountOfNextLevelSubFolders (int index)
        {
            if (isLastCell (index, flattenedFolderList.Count)) {
                return 0;
            }
            var rootFolderindex = index;
            int count = 0;
            if (HasSubFolders (index)) {
                if (isLastCell (index + 1, flattenedFolderList.Count)) {
                    return 1;
                }
                index++;
            }
            while (flattenedFolderList [rootFolderindex].indentLevel != flattenedFolderList [index].indentLevel) {
                count++;
                index++;
                if (isLastCell (index, flattenedFolderList.Count) && flattenedFolderList [rootFolderindex].indentLevel != flattenedFolderList [index].indentLevel) {
                    count++;
                    return count;
                }
            }
            return count;
        }

        public bool isLastCell (int index, int folderCount)
        {
            if (index == folderCount - 1) {
                return true;
            }
            return false;
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

        public McFolder GetFolder (int index, UIView parentView)
        {
            if (recentView == parentView) {
                return recentFolderList [index];
            } else {
                return Folders.GetFolderByFolderID (flattenedFolderList [index].folderID);
            }
        }

        public void UpdateLastAccessed ()
        {
            var list = McFolder.QueryByMostRecentlyAccessedFolders (account.Id);
            list.Reverse ();
            recentFolderList = list.Take (3).ToList ();
        }
    }
}
