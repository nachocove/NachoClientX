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
using NachoCore.ActiveSync;


namespace NachoClient.iOS
{
    public partial class FoldersViewController : UIViewController, INachoFolderChooser
    {
        public FoldersViewController (IntPtr handle) : base (handle)
        {
        }

        protected McAccount account;
        protected bool hasRecents = false;
        protected UILabel recentLabel;
        protected UILabel defaultsLabel;
        protected UILabel yourFoldersLabel;
        protected UIView topView;
        protected UIView recentView;
        protected UIView defaultsView;
        protected UIView yourFoldersView;
        protected UIScrollView scrollView;
        protected int rootFolderCount;
        protected int topFolderCount;
        const int MAX_RECENT_FOLDERS = 3;

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
            CreateView ();
            ConfigureFolders ();

            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_FolderSetChanged == s.Status.SubKind) {
                    ClearLists ();
                    ConfigureFolders ();
                    ClearViews ();
                    ConfigureView ();
                }
            };
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
            ClearViews ();
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if ("FoldersToMessageList" == segue.Identifier) {
                var holder = (SegueHolder)sender;
                var messageList = new NachoEmailMessages ((McFolder)holder.value);
                var messageListViewController = (MessageListViewController)segue.DestinationViewController;
                messageListViewController.SetEmailMessages (messageList);
                return;
            }
            if ("SegueToHotList" == segue.Identifier) {
                return;
            }
            if ("SegueToDeferredList" == segue.Identifier) {
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        UINavigationBar navbar = new UINavigationBar ();

        protected void CreateView ()
        {
            float yOffset = 20f;
            scrollView = new UIScrollView (new RectangleF (0, 0, View.Frame.Width, View.Frame.Height));

            if (modal) {
                navbar.Frame = new RectangleF (0, 0, View.Frame.Width, 64);
                View.Add (navbar);
                navbar.BackgroundColor = A.Color_NachoGreen;
                navbar.Translucent = false;
                UINavigationItem title = new UINavigationItem ("Move To");
                navbar.SetItems (new UINavigationItem[]{ title }, false);
                UIBarButtonItem cancelButton = new UIBarButtonItem ();
                Util.SetOriginalImageForButton (cancelButton, "icn-close");

                navbar.TopItem.LeftBarButtonItem = cancelButton;
                cancelButton.Clicked += (object sender, EventArgs e) => {
                    DismissViewController (true, null);
                };
                yOffset += navbar.Frame.Height;
            } else {
                NavigationItem.Title = "Mail";
                NavigationController.NavigationBar.Translucent = false;
            }

            float marginPadding = 15f;

            topView = new UIView (new RectangleF (marginPadding / 2, yOffset, View.Frame.Width - marginPadding, 44));
            topView.Layer.CornerRadius = 4;
            topView.BackgroundColor = UIColor.White;
            yOffset += topView.Frame.Height;

            yOffset += 20;
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
            yOffset += yourFoldersLabel.Frame.Height;

            yOffset += 5;
            yourFoldersView = new UIView (new RectangleF (marginPadding / 2, yOffset, View.Frame.Width - marginPadding, 44));
            yourFoldersView.Layer.CornerRadius = 4;
            yourFoldersView.BackgroundColor = UIColor.White;
            yOffset += yourFoldersView.Frame.Height;

            yOffset += 20;
            scrollView.Add (topView);
            scrollView.Add (recentLabel);
            scrollView.Add (recentView);

            scrollView.Add (defaultsLabel);
            scrollView.Add (defaultsView);
            scrollView.Add (yourFoldersLabel);
            scrollView.Add (yourFoldersView);
            scrollView.BackgroundColor = A.Color_NachoBackgroundGray;
            scrollView.ContentSize = new SizeF (View.Frame.Width, yOffset);
            View.Add (scrollView);
            if (modal) {
                View.BringSubviewToFront (navbar);
            }
        }

        protected void ConfigureView ()
        {
            if (!modal) {
                topFolderCount = 0;
                var folder = McFolder.GetDefaultInboxFolder (account.Id);
                if (null != folder) {
                    CreateTopFolderCell ("Inbox", topFolderCount, true, () => {
                        PerformSegue ("FoldersToMessageList", new SegueHolder (folder));
                    });
                    topFolderCount += 1;
                }
                CreateTopFolderCell ("Hot List", topFolderCount, true, () => {
                    PerformSegue ("SegueToHotList", new SegueHolder (null));
                });
                topFolderCount += 1;
                CreateTopFolderCell ("Deferred Messages", topFolderCount, false, () => {
                    PerformSegue ("SegueToDeferredList", new SegueHolder (null));
                });
                topFolderCount += 1;
            }

            UpdateLastAccessed ();
            if (0 != recentFolderList.Count) {
                recentLabel.Hidden = false;
                recentView.Hidden = false;
                hasRecents = true;
            } 

            var index = 0;
            foreach (var folder in recentFolderList) {
                CreateRecentFolderCell (folder, index);
                index++;
            }

            index = 0;
            foreach (var f in nestedFolderList) {
                CreateFolderCell (0, defaultsView, HasSubFolders (f), false, f);
                index++;
            }

            index = 0;
            foreach (var f in yourFolderList) {
                CreateFolderCell (0, yourFoldersView, HasSubFolders (f), false, f);
                index++;
            }
                
            LayoutView ();
        }

        protected void LayoutView ()
        {
            var yOffset = 0f;
            if (modal) {
                yOffset += 64;
            } 

            var selectedDefaultButtons = McMutables.GetOrCreate (McAccount.GetDeviceAccount ().Id, "FoldersDefaultsSelectedButtons", "DefaultsSelectedButtons", null);
            var selectedYourFoldersButtons = McMutables.GetOrCreate (McAccount.GetDeviceAccount ().Id, "FoldersYourFoldersSelectedButtons", "YourFoldersSelectedButtons", null);
            if (null != selectedDefaultButtons) {
                List<string> selectedButtons = selectedDefaultButtons.Split (':').ToList ();
                UpdateVisibleCells (defaultsView, nestedFolderList, selectedButtons);
            } else { 
                UpdateVisibleCells (defaultsView, nestedFolderList, null);
            }
            if (null != selectedYourFoldersButtons) {
                List<string> selectedButtons = selectedYourFoldersButtons.Split (':').ToList ();
                UpdateVisibleCells (yourFoldersView, yourFolderList, selectedButtons);
            } else { 
                UpdateVisibleCells (yourFoldersView, yourFolderList, null);
            }

            defaultCellsOffset = 0;

            yOffset += 24;

            if (!modal) {
                ViewFramer.Create (topView).Y (yOffset).Height (topFolderCount * 44);
                yOffset = topView.Frame.Bottom + 24;
            }

            if (hasRecents) {
                ViewFramer.Create (recentLabel).Y (yOffset);
                ViewFramer.Create (recentView).Y (yOffset + 35).Height (recentFolderList.Count * 44);
                yOffset = recentView.Frame.Bottom + 24;
            }

            ViewFramer.Create (defaultsLabel).Y (yOffset);
            yOffset += 35;
            LayoutCells (defaultsView, nestedFolderList);

            var folderListHeight = defaultCellsOffset * 44;
            ViewFramer.Create (defaultsView).Y (yOffset).Height (folderListHeight);
            yOffset += folderListHeight;

            defaultCellsOffset = 0;

            yOffset += 24;
            ViewFramer.Create (yourFoldersLabel).Y (yOffset);
            yOffset += 35;
            LayoutCells (yourFoldersView, yourFolderList);

            folderListHeight = defaultCellsOffset * 44;
            ViewFramer.Create (yourFoldersView).Y (yOffset).Height (folderListHeight);
            yOffset += folderListHeight;
           
            if (!modal) {
                yOffset += 64 + 24;
            }
            yOffset += 45;
            scrollView.ContentSize = new SizeF (View.Frame.Width, yOffset);

            HideLastLine (defaultsView);
            HideLastLine (yourFoldersView);

        }

        protected int defaultCellsOffset = 0;

        public void LayoutCells (UIView parentView, List<FolderStruct> folders)
        {
            foreach (var f in folders) {
                var cell = parentView.ViewWithTag (f.folderID + 10000) as UIView;
                if (false == cell.Hidden) {
                    cell.Frame = new RectangleF (cell.Frame.X, 44 * defaultCellsOffset, cell.Frame.Width, 44);
                    cell.ViewWithTag (cell.Tag + 20000).Hidden = false;
                    defaultCellsOffset++;
                    if (HasSubFolders (f)) {
                        LayoutCells (parentView, f.subFolders);
                    }
                } else if (true == cell.Hidden) {
                    MatchParentY (f, parentView);
                }
            }
        }

        public void MatchParentY (FolderStruct folder, UIView parentView)
        {
            var parentFolder = GetParentFolder (GetFolder (folder));
            var parentCell = parentView.ViewWithTag (parentFolder.Id + 10000) as UIView;
            var cell = parentView.ViewWithTag (folder.folderID + 10000) as UIView;
            cell.Frame = new RectangleF (cell.Frame.X, parentCell.Frame.Y, cell.Frame.Width, 44);
        }

        protected void CreateTopFolderCell (String name, int index, bool addSeparator, Action action)
        {
            UIView cell = new UIView (new RectangleF (5, 44 * index, recentView.Frame.Width - 10, 44));
            cell.BackgroundColor = UIColor.White;
            var cellTap = new UITapGestureRecognizer ();
            cellTap.AddTarget (() => {
                action ();
            });
            cell.AddGestureRecognizer (cellTap);

            UILabel label = new UILabel (new RectangleF (52, 0, cell.Frame.Width - 52, 44));
            label.Text = name;
            label.Font = A.Font_AvenirNextMedium14;
            label.TextColor = A.Color_NachoDarkText;
            cell.Add (label);

            UIImageView imageView = new UIImageView (new RectangleF (13, cell.Frame.Height / 2 - 14, 24, 24));
            imageView.Image = UIImage.FromBundle ("folder-folder");
            cell.Add (imageView);

            if (addSeparator) {
                var line = Util.AddHorizontalLineView (52, 43, cell.Frame.Width - 47, A.Color_NachoBorderGray);
                cell.Add (line);
            }

            topView.Add (cell);
        }

        protected void CreateRecentFolderCell (McFolder folder, int index)
        {
            UIView cell = new UIView (new RectangleF (5, 44 * index, recentView.Frame.Width - 10, 44));
            cell.BackgroundColor = UIColor.White;
            var cellTap = new UITapGestureRecognizer ();
            cellTap.AddTarget (() => {
                folder = folder.UpdateSet_LastAccessed (DateTime.UtcNow);
                UpdateLastAccessed ();
                if (modal) {
                    FolderSelected (folder);
                } else {
                    PerformSegue ("FoldersToMessageList", new SegueHolder (folder));
                }
            });
            cell.AddGestureRecognizer (cellTap);

            UILabel label = new UILabel (new RectangleF (52, 0, cell.Frame.Width - 52, 44));
            label.Text = folder.DisplayName;
            label.Font = A.Font_AvenirNextMedium14;
            label.TextColor = A.Color_NachoDarkText;
            cell.Add (label);

            UIImageView imageView = new UIImageView (new RectangleF (13, cell.Frame.Height / 2 - 14, 24, 24));
            imageView.Image = UIImage.FromBundle ("folder-folder");
            cell.Add (imageView);

            if (folder != recentFolderList.Last ()) {
                var line = Util.AddHorizontalLineView (52, 43, cell.Frame.Width - 47, A.Color_NachoBorderGray);
                line.Tag = folder.Id;
                cell.Add (line);
            }

            recentView.Add (cell);
        }

        public void ClearViews ()
        {
            foreach (var v in defaultsView.Subviews) {
                v.RemoveFromSuperview ();
            }
            foreach (var v in yourFoldersView.Subviews) {
                v.RemoveFromSuperview ();
            }
        }

        protected void CreateFolderCell (int subLevel, UIView parentView, bool subFolders, bool isHidden, FolderStruct folderStruct)
        {
            var tag = folderStruct.folderID + 10000;

            var indentation = subLevel * 10;
            UIView cell = new UIView (new RectangleF (5 + indentation, 0, parentView.Frame.Width - 10 - indentation, 44));
            cell.BackgroundColor = UIColor.White;
            var cellTap = new UITapGestureRecognizer ();
            cellTap.AddTarget (() => {
                var folder = GetFolder (folderStruct);
                folder = folder.UpdateSet_LastAccessed (DateTime.UtcNow);
                UpdateLastAccessed ();
                if (modal) {
                    FolderSelected (folder);
                } else {
                    PerformSegue ("FoldersToMessageList", new SegueHolder (folder));
                }
            });
            cell.AddGestureRecognizer (cellTap);

            UILabel label = new UILabel (new RectangleF (52, 0, cell.Frame.Width - 52, 44));
            label.Text = folderStruct.folderName;
            label.Font = A.Font_AvenirNextMedium14;
            label.TextColor = A.Color_NachoDarkText;
            cell.Add (label);

            UIImageView imageView = new UIImageView (new RectangleF (13, cell.Frame.Height / 2 - 14, 24, 24));
            imageView.Image = UIImage.FromBundle ("folder-folder");
            cell.Add (imageView);

            var line = Util.AddHorizontalLineView (52, 43, cell.Frame.Width - 47, A.Color_NachoBorderGray);
            line.Tag = tag + 20000;
            cell.Add (line);

            if (subFolders) {
                CreateNestedCells (folderStruct, subLevel, parentView);

                UIImageView buttonImageView = new UIImageView (new RectangleF (cell.Frame.Width - 31, cell.Frame.Height / 2 - 10, 18, 18));
                buttonImageView.Image = UIImage.FromBundle ("gen-readmore");
                buttonImageView.Tag = tag + 30000;
                cell.Add (buttonImageView);

                UIButton expandButton = new UIButton (UIButtonType.RoundedRect);
                expandButton.Frame = new RectangleF (cell.Frame.Width - 40, cell.Frame.Height / 2 - 18, 36, 36);
                expandButton.TintColor = UIColor.Clear;
                expandButton.Tag = tag + 10000;
                expandButton.TouchUpInside += (sender, e) => {
                    parentView.BringSubviewToFront (cell);
                    ToggleHiddenCells (folderStruct, parentView);
                };
                cell.Add (expandButton);
            }
            cell.Hidden = isHidden;
            cell.Tag = tag;
            parentView.Add (cell);
        }

        public void ToggleHiddenCells (FolderStruct folder, UIView parentView)
        {
            var expandButton = parentView.ViewWithTag (20000 + folder.folderID) as UIButton;
            int count = folder.subFolders.Count;
            if (!expandButton.Selected) {
                foreach (var subFolder in folder.subFolders) {
                    var cell = parentView.ViewWithTag (subFolder.folderID + 10000) as UIView;
                    cell.Hidden = false;
                }
                expandButton.Selected = true;
                var buttonImage = parentView.ViewWithTag (40000 + folder.folderID) as UIImageView;
                buttonImage.Image = UIImage.FromBundle ("gen-readmore-active");
                if (defaultsView == parentView) {
                    UpdateSelectedButtonsList (20000 + folder.folderID, true, "DefaultsSelectedButtons");
                } else {
                    UpdateSelectedButtonsList (20000 + folder.folderID, true, "YourFoldersSelectedButtons");
                }
                UIView.Animate (.2, 0, UIViewAnimationOptions.CurveLinear,
                    () => {
                        LayoutView ();
                    },
                    () => {
                    }
                );
                return;
            } else {
                HideAllSubFolders (folder, parentView);
                expandButton.Selected = false;
                var buttonImage = parentView.ViewWithTag (40000 + folder.folderID) as UIImageView;
                buttonImage.Image = UIImage.FromBundle ("gen-readmore");
                if (defaultsView == parentView) {
                    UpdateSelectedButtonsList (20000 + folder.folderID, false, "DefaultsSelectedButtons");
                } else {
                    UpdateSelectedButtonsList (20000 + folder.folderID, false, "YourFoldersSelectedButtons");
                }
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

        public void HideLastLine (UIView parentView)
        {
            FolderStruct lastFolder;
            if (parentView == defaultsView) {
                lastFolder = nestedFolderList.Last ();
            } else {
                lastFolder = yourFolderList.Last ();
            }
            var LastCell = GetLastCell (lastFolder, parentView);
            LastCell.ViewWithTag (LastCell.Tag + 20000).Hidden = true;
        }

        public UIView GetLastCell (FolderStruct folder, UIView parentView)
        {
            var cell = parentView.ViewWithTag (folder.folderID + 10000);
            if (null != cell && false == cell.Hidden) {
                if (HasSubFolders (folder)) {
                    if (true == (cell.ViewWithTag (cell.Tag + 10000) as UIButton).Selected) {
                        return GetLastCell (folder.subFolders.Last (), parentView);
                    }
                }
                return cell;
            }
            return null;
        }

        public void HideAllSubFolders (FolderStruct folder, UIView parentView)
        {
            foreach (var subFolder in folder.subFolders) {
                var cell = parentView.ViewWithTag (subFolder.folderID + 10000) as UIView;
                cell.Hidden = true;
                if (HasSubFolders (subFolder)) {
                    HideAllSubFolders (subFolder, parentView);
                }
            }
        }

        public void CreateNestedCells (FolderStruct folder, int subLevel, UIView parentView)
        {
            var subFolders = folder.subFolders;
            int i = 0;
            foreach (var f in subFolders) {
                CreateFolderCell (subLevel + 1, parentView, HasSubFolders (f), true, f);
                i++;
            }
        }

        string selectedButtonsString = "";

        public void UpdateSelectedButtonsList (int tag, bool add, string stringName)
        {
            if (add) {
                if ("" == selectedButtonsString) {
                    selectedButtonsString = ":" + tag.ToString ();
                } else {
                    selectedButtonsString += ":" + tag.ToString ();
                }
            } else {
                var stringToRemove = ":" + tag.ToString ();
                selectedButtonsString = selectedButtonsString.Replace (stringToRemove, "");
            }

            McMutables.Set (McAccount.GetDeviceAccount ().Id, "Folders" + stringName, stringName, selectedButtonsString);
        }

        public void UpdateVisibleCells (UIView parentView, List<FolderStruct> folders, List<string> selectedButtons)
        {
            if (null != selectedButtons) {
                foreach (var tag in selectedButtons) {
                    if ("" != tag) {
                        int numTag = Convert.ToInt32 (tag);
                        int folderID = numTag - 20000;
                        var button = parentView.ViewWithTag (numTag) as UIButton;
                        if (null != button) {
                            button.Selected = true;
                            var buttonImage = parentView.ViewWithTag (numTag + 20000) as UIImageView;
                            buttonImage.Image = UIImage.FromBundle ("gen-readmore-active");
                            var folder = GetFolderStructById (folderID, folders);
                            if (IsParentCellButtonSelected (GetFolder (folder), selectedButtons)) {
                                foreach (var subFolder in folder.subFolders) {
                                    var cell = parentView.ViewWithTag (subFolder.folderID + 10000) as UIView;
                                    cell.Hidden = false;
                                }
                            }
                        }
                    }
                }
            } else {
                foreach (var folder in folders) {
                    var cell = parentView.ViewWithTag (folder.folderID + 10000);
                    cell.Hidden = false;
                }
            }
        }

        public bool IsParentCellButtonSelected (McFolder folder, List<string> selectedButtons)
        {
            var parentFolder = GetParentFolder (folder);
            if (null != parentFolder) {
                return ((IsParentCellButtonSelected (parentFolder, selectedButtons)) && (selectedButtons.Contains ((parentFolder.Id + 20000).ToString ())));
            }
            return true;
        }

        public FolderStruct GetFolderStructById (int Id, List<FolderStruct> folders)
        {
            foreach (var folder in folders) {
                if (Id == folder.folderID) {
                    return folder;
                }
                if (HasSubFolders (folder)) {
                    var child = GetFolderStructById (Id, folder.subFolders);
                    if (null != child) {
                        return child;
                    }
                }
            }
            return null;
        }

        NachoFolders Folders;
        public List<McFolder> foldersToMcFolders = new List<McFolder> ();
        public List<FolderStruct> nestedFolderList = new List<FolderStruct> ();
        public List<FolderStruct> yourFolderList = new List<FolderStruct> ();
        public List<McFolder> recentFolderList = new List<McFolder> ();

        public void ClearLists ()
        {
            foldersToMcFolders.Clear ();
            nestedFolderList.Clear ();
            yourFolderList.Clear ();
        }

        public void ConfigureFolders ()
        {
            Folders = new NachoFolders (NachoFolders.FilterForEmail);
            ConvertFoldersToMcFolders ();
            CreateNestedFolderList ();
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
                if (McFolder.AsRootServerId == folder.ParentId) {
                    int folderID = folder.Id;
                    string fname = folder.DisplayName;
                    Xml.FolderHierarchy.TypeCode ftype = folder.Type;
                    List<FolderStruct> subFolders = new List<FolderStruct> ();
                    subFolders = GetSubFolders (folder.Id, folder.AccountId, folder.ServerId, 0);

                    if (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedMail_12 == folder.Type || NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1 == folder.Type) {
                        yourFolderList.Add (new FolderStruct (folderID, subFolders, fname, ftype, 10000));
                    } else {
                        nestedFolderList.Add (new FolderStruct (folderID, subFolders, fname, ftype, 10000));
                    }
                }
            }
            SortDetaultsFoldersList ();
        }

        public void SortDetaultsFoldersList ()
        {
            foreach (var folder in nestedFolderList) {
                switch (folder.type) {
                case Xml.FolderHierarchy.TypeCode.DefaultInbox_2: 
                    folder.orderNumber = 1;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultDrafts_3:
                    folder.orderNumber = 2;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultDeleted_4:
                    folder.orderNumber = 3;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultSent_5:
                    folder.orderNumber = 4;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultOutbox_6:
                    folder.orderNumber = 5;
                    break;
                default:
                    break;
                }
            }
            nestedFolderList = nestedFolderList.OrderBy (x => x.orderNumber).ToList ();
        }

        public List<FolderStruct> GetSubFolders (int fID, int accountID, string serverID, int indentLevel)
        {
            indentLevel += 1;
            List<FolderStruct> subFolders = new List<FolderStruct> ();
            List<McFolder> folds = new List<McFolder> ();
            folds = McFolder.QueryByParentId (accountID, serverID);

            foreach (McFolder f in folds) {
                subFolders.Add (new FolderStruct (f.Id, GetSubFolders (f.Id, f.AccountId, f.ServerId, indentLevel), f.DisplayName, f.Type, 10000));
            }
            return subFolders;
        }

        public bool HasSubFolders (FolderStruct folder)
        {
            if (0 != folder.subFolders.Count) {
                return true;
            }
            return false;
        }

        public class FolderStruct
        {
            public int folderID { get; set; }

            public string folderName { get; set; }

            public List <FolderStruct> subFolders { get; set; }

            public Xml.FolderHierarchy.TypeCode type { get; set; }

            public int orderNumber { get; set; }

            public FolderStruct ()
            {

            }

            public FolderStruct (int fid, List<FolderStruct> sf, string fn, Xml.FolderHierarchy.TypeCode t, int on)
            {
                folderID = fid;
                subFolders = sf;
                folderName = fn;
                type = t;
                orderNumber = on;
            }
        }

        public McFolder GetFolder (FolderStruct folder)
        {
            return Folders.GetFolderByFolderID (folder.folderID);
        }

        public McFolder GetParentFolder (McFolder folder)
        {
            return McFolder.ServerEndQueryByServerId (account.Id, folder.ParentId);
        }

        public void UpdateLastAccessed ()
        {
            var list = McFolder.QueryByMostRecentlyAccessedFolders (account.Id);
            recentFolderList = list.Take (MAX_RECENT_FOLDERS).ToList ();
        }

        protected object cookie;
        protected bool modal;
        protected INachoFolderChooserParent owner;

        public void SetOwner (INachoFolderChooserParent owner, object cookie)
        {
            this.owner = owner;
            this.cookie = cookie;
        }

        public void DismissFolderChooser (bool animated, NSAction action)
        {
            owner = null;
            cookie = null;
            DismissViewController (animated, action);
        }

        public void FolderSelected (McFolder folder)
        {
            owner.FolderSelected (this, folder, cookie);
        }

        public void SetModal (bool modal)
        {
            this.modal = modal;
        }
            
    }
}
