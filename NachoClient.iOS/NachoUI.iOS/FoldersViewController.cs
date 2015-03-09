// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;
using UIKit;
using EventKit;
using System.IO;
using System.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.ActiveSync;


namespace NachoClient.iOS
{
    public partial class FoldersViewController : UIViewController, INachoFolderChooser, INachoMessageEditorParent
    {
        public FoldersViewController (IntPtr handle) : base (handle)
        {
        }

        protected bool foldersNeedRefresh;
        protected McAccount account;
        protected bool hasRecents = false;
        protected UILabel recentLabel;
        protected UILabel defaultsLabel;
        protected UILabel yourFoldersLabel;
        protected UIView titleView;
        protected UIView topView;
        protected UIView recentView;
        protected UIView defaultsView;
        protected UIView yourFoldersView;
        protected UIScrollView scrollView;
        protected int rootFolderCount;
        protected int topFolderCount;
        const int MAX_RECENT_FOLDERS = 3;

        protected UIColor textLabelColor;
        protected UIColor textColor;
        protected UIColor separatorColor;
        protected UIColor cellBGColor;
        protected UIColor cellHighlightedColor;
        protected UIColor scrollViewBGColor;
        protected UIColor borderColor;

        protected UIImage folderIcon;
        protected UIImage moreIcon;
        protected UIImage moreIconSelected;
        protected UIBarButtonItem composeButton;

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Uncomment to hide <More
            // if (null != NavigationItem) {
            //     NavigationItem.SetHidesBackButton (true, false);
            // }

            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
            CreateView ();
            ConfigureFolders ();
            ConfigureView ();

            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_FolderSetChanged == s.Status.SubKind) {
                    RefreshFoldersIfVisible ();
                }
            };
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            MaybeRefreshFolders ();
        }

        protected void RefreshFoldersIfVisible ()
        {
            foldersNeedRefresh = true;
            if (!this.IsVisible ()) {
                return;
            }
            MaybeRefreshFolders ();
        }

        protected void MaybeRefreshFolders ()
        {
            if (foldersNeedRefresh) {
                foldersNeedRefresh = false;
                ClearLists ();
                ConfigureFolders ();
                ClearViews ();
                ConfigureView ();
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            foldersNeedRefresh = true; // update the 'recent' list

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
            if (segue.Identifier == "SegueToCompose") {
                var vc = (MessageComposeViewController)segue.DestinationViewController;
                vc.SetAction (null, null);
                vc.SetOwner (this);
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        protected void CreateView ()
        {
            nfloat yOffset = 64f;
            scrollView = new UIScrollView (new CGRect (0, 0, View.Frame.Width, View.Frame.Height));
            ConfigureColors ();

            if (modal) {
                titleView = new UIView ();
                titleView.Frame = new CGRect (0, 0, View.Frame.Width, 64);
                titleView.ClipsToBounds = true;
                titleView.BackgroundColor = A.Color_NachoGreen;

                var navBar = new UINavigationBar (new CGRect (0, 20, View.Frame.Width, 44));
                navBar.BarStyle = UIBarStyle.Default;
                navBar.Opaque = true;
                navBar.Translucent = false;

                var navItem = new UINavigationItem ();
                navItem.Title = "Move to Folder";
                using (var image = UIImage.FromBundle ("modal-close")) {
                    var dismissButton = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, null);
                    dismissButton.Clicked += (object sender, EventArgs e) => {
                        DismissViewController (true, null);
                    };
                    navItem.LeftBarButtonItem = dismissButton;
                }
                navBar.Items = new UINavigationItem[] { navItem };

                titleView.AddSubview (navBar);

                UIView sectionSeparator = new UIView (new CGRect (0, yOffset - .5f, View.Frame.Width, .5f));
                sectionSeparator.BackgroundColor = separatorColor;
                titleView.AddSubview (sectionSeparator);

                yOffset = sectionSeparator.Frame.Bottom + 20;

                View.AddSubview (titleView);

            } else {
                NavigationItem.Title = "Mail";
                NavigationController.NavigationBar.Translucent = false;

                var composeButton = new UIBarButtonItem ();
                Util.SetAutomaticImageForButton (composeButton, "contact-newemail");
                composeButton.Clicked += (object sender, EventArgs e) => {
                    PerformSegue ("SegueToCompose", new SegueHolder (null));
                };
                NavigationItem.RightBarButtonItem = composeButton;
            }

            nfloat marginPadding = 15f;

            topView = new UIView (new CGRect (marginPadding / 2, yOffset, View.Frame.Width - marginPadding, 44));
            topView.Layer.CornerRadius = 6;
            topView.Layer.BorderColor = borderColor.CGColor;
            topView.Layer.BorderWidth = .5f;
            topView.BackgroundColor = cellBGColor;
            yOffset += topView.Frame.Height;

            yOffset += 20;
            recentLabel = new UILabel (new CGRect (20, yOffset, 160, 20));
            recentLabel.Text = "Recent Folders";
            recentLabel.Font = A.Font_AvenirNextDemiBold17;
            recentLabel.TextColor = textLabelColor;
            recentLabel.Hidden = true;
            yOffset += recentLabel.Frame.Height;

            yOffset += 15;
            recentView = new UIView (new CGRect (marginPadding / 2, yOffset, View.Frame.Width - marginPadding, 44));
            recentView.Layer.CornerRadius = 6;
            recentView.Layer.BorderColor = borderColor.CGColor;
            recentView.Layer.BorderWidth = .5f;
            recentView.BackgroundColor = cellBGColor;
            recentView.Hidden = true;
            yOffset += recentView.Frame.Height;

            yOffset += 20;
            defaultsLabel = new UILabel (new CGRect (20, yOffset, 160, 20));
            defaultsLabel.Text = "Default Folders";
            defaultsLabel.Font = A.Font_AvenirNextDemiBold17;
            defaultsLabel.TextColor = textLabelColor;
            yOffset += defaultsLabel.Frame.Height;

            yOffset += 5;
            defaultsView = new UIView (new CGRect (marginPadding / 2, yOffset, View.Frame.Width - marginPadding, 44));
            defaultsView.Layer.CornerRadius = 6;
            defaultsView.Layer.BorderColor = borderColor.CGColor;
            defaultsView.Layer.BorderWidth = .5f;
            defaultsView.BackgroundColor = cellBGColor;
            yOffset += defaultsView.Frame.Height;

            yOffset += 20;
            yourFoldersLabel = new UILabel (new CGRect (20, yOffset, 160, 20));
            yourFoldersLabel.Text = "Your Folders";
            yourFoldersLabel.Font = A.Font_AvenirNextDemiBold17;
            yourFoldersLabel.TextColor = textLabelColor;
            yOffset += yourFoldersLabel.Frame.Height;

            yOffset += 5;
            yourFoldersView = new UIView (new CGRect (marginPadding / 2, yOffset, View.Frame.Width - marginPadding, 44));
            yourFoldersView.Layer.CornerRadius = 6;
            yourFoldersView.Layer.BorderColor = borderColor.CGColor;
            yourFoldersView.Layer.BorderWidth = .5f;
            yourFoldersView.BackgroundColor = cellBGColor;
            yOffset += yourFoldersView.Frame.Height;

            yOffset += 20;
            scrollView.Add (topView);
            scrollView.Add (recentLabel);
            scrollView.Add (recentView);

            scrollView.Add (defaultsLabel);
            scrollView.Add (defaultsView);
            scrollView.Add (yourFoldersLabel);
            scrollView.Add (yourFoldersView);
            scrollView.BackgroundColor = scrollViewBGColor;
            scrollView.ContentSize = new CGSize (View.Frame.Width, yOffset);
            View.Add (scrollView);
            if (modal) {
                View.BringSubviewToFront (titleView);
            }
        }

        protected void ConfigureColors ()
        {
            if (modal) {
                textLabelColor = UIColor.White;
                textColor = UIColor.White;
                separatorColor = UIColor.LightGray.ColorWithAlpha (.6f);
                cellBGColor = A.Color_NachoGreen;
                cellHighlightedColor = UIColor.DarkGray;
                scrollViewBGColor = A.Color_NachoGreen;
                borderColor = UIColor.Clear;
                folderIcon = UIImage.FromBundle ("modal-folder");
                moreIcon = UIImage.FromBundle ("modal-readmore");
                moreIconSelected = UIImage.FromBundle ("modal-readmore-active");
            } else {
                textLabelColor = A.Color_NachoGreen;
                textColor = A.Color_NachoDarkText;
                separatorColor = A.Color_NachoBorderGray;
                cellBGColor = UIColor.White;
                cellHighlightedColor = UIColor.LightGray;
                scrollViewBGColor = A.Color_NachoBackgroundGray;
                borderColor = A.Color_NachoBorderGray;
                folderIcon = UIImage.FromBundle ("folder-folder");
                moreIcon = UIImage.FromBundle ("gen-readmore");
                moreIconSelected = UIImage.FromBundle ("gen-readmore-active");
            }
        }

        protected void ConfigureView ()
        {
            if (!modal) {
                topFolderCount = 0;
                var folder = McFolder.GetDefaultInboxFolder (account.Id);
                if (null != folder) {
                    CreateTopFolderCell (folder.DisplayName, topFolderCount, true, () => {
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
                
            foreach (var f in nestedFolderList) {
                CreateFolderCell (0, defaultsView, HasSubFolders (f), false, f);
            }
                
            foreach (var f in yourFolderList) {
                CreateFolderCell (0, yourFoldersView, HasSubFolders (f), false, f);
            }

            LayoutView ();
        }

        protected void LayoutView ()
        {
            nfloat yOffset = 24f;
            if (modal) {
                yOffset = 85f;
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

            ViewFramer.Create (topView).Y (yOffset).Height (topFolderCount * 44);
            yOffset = topView.Frame.Bottom + 24;

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
            scrollView.ContentSize = new CGSize (View.Frame.Width, yOffset);

            HideLastLine (defaultsView);
            HideLastLine (yourFoldersView);

        }

        protected int defaultCellsOffset = 0;

        public void LayoutCells (UIView parentView, List<FolderStruct> folders)
        {
            foreach (var f in folders) {
                var cell = parentView.ViewWithTag (f.folderID + 10000) as UIView;
                if (!cell.Hidden) {
                    cell.Frame = new CGRect (cell.Frame.X, 44 * defaultCellsOffset, cell.Frame.Width, 44);
                    cell.ViewWithTag (cell.Tag + 20000).Hidden = false;
                    defaultCellsOffset++;
                    if (HasSubFolders (f)) {
                        LayoutCells (parentView, f.subFolders);
                    }
                } else if (cell.Hidden) {
                    MatchParentY (f, parentView);
                }
            }
        }

        public void MatchParentY (FolderStruct folder, UIView parentView)
        {
            var parentFolder = GetParentFolder (GetFolder (folder));
            var parentCell = parentView.ViewWithTag (parentFolder.Id + 10000) as UIView;
            var cell = parentView.ViewWithTag (folder.folderID + 10000) as UIView;
            cell.Frame = new CGRect (cell.Frame.X, parentCell.Frame.Y, cell.Frame.Width, 44);
        }

        protected void CreateTopFolderCell (String name, int index, bool addSeparator, Action action)
        {
            UIButton cell = new UIButton (UIButtonType.RoundedRect);
            cell.Frame = new CGRect (5, 44 * index, recentView.Frame.Width - 10, 44);
            cell.BackgroundColor = cellBGColor;
            cell.AccessibilityLabel = name;
            cell.SetImage (Util.DrawButtonBackgroundImage (cellHighlightedColor, cell.Frame.Size), UIControlState.Highlighted);
            cell.TouchUpInside += (sender, e) => {
                action ();
            };

            UILabel label = new UILabel (new CGRect (52, 0, cell.Frame.Width - 52, 44));
            label.Text = name;
            label.Font = A.Font_AvenirNextMedium14;
            label.TextColor = textColor;
            cell.Add (label);

            UIImageView imageView = new UIImageView (new CGRect (13, cell.Frame.Height / 2 - 14, 24, 24));
            imageView.Image = folderIcon;
            cell.Add (imageView);

            if (addSeparator) {
                var line = Util.AddHorizontalLine (52, 43, cell.Frame.Width - 47, separatorColor);
                cell.Add (line);
            }

            topView.Add (cell);
        }

        protected void CreateRecentFolderCell (McFolder folder, int index)
        {
            UIButton cell = new UIButton (UIButtonType.RoundedRect);
            cell.Frame = new CGRect (5, 44 * index, recentView.Frame.Width - 10, 44);
            cell.BackgroundColor = cellBGColor;
            cell.SetImage (Util.DrawButtonBackgroundImage (cellHighlightedColor, cell.Frame.Size), UIControlState.Highlighted);
            cell.TouchUpInside += (sender, e) => {
                folder = folder.UpdateSet_LastAccessed (DateTime.UtcNow);
                UpdateLastAccessed ();
                if (modal) {
                    FolderSelected (folder);
                } else {
                    PerformSegue ("FoldersToMessageList", new SegueHolder (folder));
                }
            };
                
            UILabel label = new UILabel (new CGRect (52, 0, cell.Frame.Width - 52, 44));
            label.Text = folder.DisplayName;
            label.Font = A.Font_AvenirNextMedium14;
            label.TextColor = textColor;
            cell.Add (label);

            UIImageView imageView = new UIImageView (new CGRect (13, cell.Frame.Height / 2 - 14, 24, 24));
            imageView.Image = folderIcon;
            cell.Add (imageView);

            if (folder != recentFolderList.Last ()) {
                var line = Util.AddHorizontalLine (52, 43, cell.Frame.Width - 47, separatorColor);
                line.Tag = folder.Id;
                cell.Add (line);
            }

            recentView.Add (cell);
        }

        public void ClearViews ()
        {
            if (null != recentView) {
                foreach (var v in recentView.Subviews) {
                    v.RemoveFromSuperview ();
                }
            }
            // Not sure how defaultsView can be null
            // unless DidDisappear is called before ViewDidLoad.
            if (null != defaultsView) {
                foreach (var v in defaultsView.Subviews) {
                    v.RemoveFromSuperview ();
                }
            }
            if (null != yourFoldersView) {
                foreach (var v in yourFoldersView.Subviews) {
                    v.RemoveFromSuperview ();
                }
            }
        }

        protected void CreateFolderCell (int subLevel, UIView parentView, bool subFolders, bool isHidden, FolderStruct folderStruct)
        {
            var tag = folderStruct.folderID + 10000;

            var indentation = subLevel * 10;
            UIButton cell = new UIButton (UIButtonType.RoundedRect);
            cell.Frame = new CGRect (5 + indentation, 0, parentView.Frame.Width - 10 - indentation, 44);
            cell.BackgroundColor = cellBGColor;
            cell.SetImage (Util.DrawButtonBackgroundImage (cellHighlightedColor, cell.Frame.Size), UIControlState.Highlighted);
            cell.TouchUpInside += (sender, e) => {
                var folder = GetFolder (folderStruct);
                folder = folder.UpdateSet_LastAccessed (DateTime.UtcNow);
                UpdateLastAccessed ();
                if (modal) {
                    FolderSelected (folder);
                } else {
                    PerformSegue ("FoldersToMessageList", new SegueHolder (folder));
                }
            };

            UILabel label = new UILabel (new CGRect (52, 0, cell.Frame.Width - 52, 44));
            label.Text = folderStruct.folderName;
            label.Font = A.Font_AvenirNextMedium14;
            label.TextColor = textColor;
            cell.Add (label);

            UIImageView imageView = new UIImageView (new CGRect (13, cell.Frame.Height / 2 - 14, 24, 24));
            imageView.Image = folderIcon;
            cell.Add (imageView);

            var line = Util.AddHorizontalLine (52, 43, cell.Frame.Width - 47, separatorColor);
            line.Tag = tag + 20000;
            cell.Add (line);

            if (subFolders) {
                CreateNestedCells (folderStruct, subLevel, parentView);

                UIImageView buttonImageView = new UIImageView (new CGRect (cell.Frame.Width - 31, cell.Frame.Height / 2 - 10, 18, 18));
                buttonImageView.Image = moreIcon;
                buttonImageView.Tag = tag + 30000;
                cell.Add (buttonImageView);

                UIButton expandButton = new UIButton (UIButtonType.RoundedRect);
                expandButton.Frame = new CGRect (cell.Frame.Width - 40, cell.Frame.Height / 2 - 18, 36, 36);
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
                buttonImage.Image = moreIconSelected;
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
                buttonImage.Image = moreIcon;
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
                if ((null == nestedFolderList) || (0 == nestedFolderList.Count)) {
                    return;
                }
                lastFolder = nestedFolderList.Last ();
            } else {
                if ((null == yourFolderList) || (0 == yourFolderList.Count)) {
                    return;
                }
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
                            buttonImage.Image = moreIconSelected;
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
            Folders = new NachoFolders (account.Id, NachoFolders.FilterForEmail);
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
            folds = McFolder.QueryVisibleChildrenOfParentId (accountID, serverID);

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
            return McFolder.QueryByServerId (account.Id, folder.ParentId);
        }

        public void UpdateLastAccessed ()
        {
            var list = McFolder.QueryByMostRecentlyAccessedVisibleFolders (account.Id);
            recentFolderList = list.Take (MAX_RECENT_FOLDERS).ToList ();
        }

        protected object cookie;
        protected bool modal;
        protected INachoFolderChooserParent owner;

        public void SetOwner (INachoFolderChooserParent owner, bool modal, object cookie)
        {
            this.owner = owner;
            this.modal = modal;
            this.cookie = cookie;
        }

        public void DismissFolderChooser (bool animated, Action action)
        {
            owner = null;
            cookie = null;
            DismissViewController (animated, action);
        }

        public void FolderSelected (McFolder folder)
        {
            owner.FolderSelected (this, folder, cookie);
        }

        // INachoMessageEditorParent
        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            NcAssert.CaseError ();
        }

        // INachoMessageEditorParent
        public void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            NcAssert.CaseError ();
        }

        // INachoMessageEditorParent
        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, null);
        }

    }
}
