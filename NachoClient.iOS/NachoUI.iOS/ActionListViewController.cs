//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using Foundation;
using CoreGraphics;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class ActionListViewController : NachoTableViewController
    {

        #region Properties

        const string ActionCellIdentifier = "ActionCellIdentifier";
        const string SubStateCellIdentifier = "SubStateCellIdentifier";

        NachoActions Actions;

        SwitchAccountButton SwitchAccountButton;
        McAction.ActionState State;
        McAccount Account;

        UIBarButtonItem EditTableButton;
        UIBarButtonItem DoneSwipingButton;
        UIBarButtonItem CancelEditingButton;

        UIBarButtonItem DeferButton;
        UIBarButtonItem DeleteButton;

        int NumberOfPreviewLines = 1;
        bool HasAppearedOnce = false;
        bool IsListeningForStatusInd = false;
        bool HasLoadedOnce = false;

        public McAction.ActionState[] SubStates;

        #endregion

        #region Constructors

        public ActionListViewController (McAction.ActionState state) : base (UITableViewStyle.Plain)
        {
            State = state;
            if (State == McAction.ActionState.Open) {
                SubStates = new McAction.ActionState[] {
                    McAction.ActionState.Deferred,
                    McAction.ActionState.Completed
                };
            }else{
                SubStates = new McAction.ActionState[] { };
            }
            Account = NcApplication.Instance.Account;
            Actions = new NachoActions (Account.Id, State);
            AutomaticallyAdjustsScrollViewInsets = false;

            EditTableButton = new UIBarButtonItem ("Edit", UIBarButtonItemStyle.Plain, EditTable);
            EditTableButton.AccessibilityLabel = "Edit";
            CancelEditingButton = new UIBarButtonItem ("Cancel", UIBarButtonItemStyle.Plain, CancelEditingTable);
            CancelEditingButton.AccessibilityLabel = "Cancel Editing";
            DoneSwipingButton = new UIBarButtonItem ("Done", UIBarButtonItemStyle.Plain, EndSwiping);
            EditTableButton.AccessibilityLabel = "Done";

            UpdateNavigationItem ();
        }

        #endregion

        #region View Lifecycle

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.RowHeight = ActionCell.PreferredHeight (NumberOfPreviewLines, A.Font_AvenirNextDemiBold17, A.Font_AvenirNextRegular14);
            TableView.SeparatorInset = new UIEdgeInsets (0.0f, 44.0f, 0.0f, 0.0f);
            TableView.AllowsMultipleSelectionDuringEditing = true;
            TableView.RegisterClassForCellReuse (typeof(ActionCell), ActionCellIdentifier);
            TableView.RegisterClassForCellReuse (typeof(SwipeTableViewCell), SubStateCellIdentifier);
        }

        public override void ViewDidLoad ()
        {
            SwitchAccountButton = new SwitchAccountButton (ShowAccountSwitcher);
            NavigationItem.TitleView = SwitchAccountButton;
            SwitchAccountButton.SetAccountImage (Account);
            base.ViewDidLoad ();
            Reload ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (NcApplication.Instance.Account.Id != Account.Id) {
                SwitchToAccount (NcApplication.Instance.Account);
            }
            if (HasAppearedOnce && !TableView.Editing) {
                Reload ();
            }
            StartListeningForStatusInd ();
            HasAppearedOnce = true;
        }

        public override void ViewDidDisappear (bool animated)
        {
            StopListeningForStatusInd ();
            base.ViewDidDisappear (animated);
        }

        protected override void Cleanup ()
        {

            EditTableButton.Clicked -= EditTable;
            CancelEditingButton.Clicked -= CancelEditingTable;
            DoneSwipingButton.Clicked -= EndSwiping;

            base.Cleanup ();
        }

        #endregion

        #region User Actions

        void ShowAccountSwitcher ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        void EditTable (object sender, EventArgs e)
        {
            StartEditingTable ();
        }

        void CancelEditingTable (object sender, EventArgs e)
        {
            CancelEditingTable ();
        }

        void EndSwiping (object sender, EventArgs e)
        {
            EndSwiping ();
        }

        void DeleteSelectedActions (object sender, EventArgs e)
        {
            // TODO:
//            NcEmailArchiver.Delete (SelectedActions ());
            CancelEditingTable ();
        }

        #endregion

        #region Reloading Data

        void Reload ()
        {
            Actions.BackgroundRefresh (HandleReloadResults);
        }

        void HandleReloadResults (bool changed, List<int> adds, List<int> deletes)
        {
            if (!HasLoadedOnce) {
                HasLoadedOnce = true;
                TableView.ReloadData ();
            } else {
                Util.UpdateTable (TableView, adds, deletes);
            }
            UpdateVisibleRows ();
        }

        void UpdateVisibleRows ()
        {
            var indexPaths = TableView.IndexPathsForVisibleRows;
            if (indexPaths != null) {
                foreach (var indexPath in indexPaths) {
                    if (indexPath.Row < Actions.Count ()) {
                        var action = Actions.ActionAt (indexPath.Row);
                        var cell = TableView.CellAt (indexPath) as ActionCell;
                        if (cell != null && action != null) {
                            cell.SetAction (action);
                        }
                    } else {
                        TableView.ReloadRows (new NSIndexPath[] { indexPath }, UITableViewRowAnimation.None);
                    }
                }
            }
        }

        #endregion

        #region Table View Delegate & Data Source

        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            return Actions.Count () + SubStates.Length;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Row < Actions.Count ()) {
                var action = Actions.ActionAt (indexPath.Row);
                var cell = tableView.DequeueReusableCell (ActionCellIdentifier) as ActionCell;
                cell.SetAction (action);
                cell.NumberOfPreviewLines = NumberOfPreviewLines;
                if (Actions.IncludesMultipleAccounts ()) {
                    cell.IndicatorColor = Util.ColorForAccount (action.AccountId);
                } else {
                    cell.IndicatorColor = null;
                }
                cell.SeparatorInset = TableView.SeparatorInset;
                if (action.IsHot) {
                    cell.CheckboxView.TintColor = UIColor.FromRGB (0xEE, 0x70, 0x5B);
                } else {
                    cell.CheckboxView.TintColor = A.Color_NachoGreen;
                }
                return cell;
            } else {
                var cell = tableView.DequeueReusableCell (SubStateCellIdentifier) as SwipeTableViewCell;
                var index = indexPath.Row - Actions.Count ();
                var state = SubStates [index];
                cell.TextLabel.Text = String.Format ("{0} Actions", NameForState (state));
                cell.TextLabel.Font = A.Font_AvenirNextRegular17;
                cell.TextLabel.TextColor = A.Color_NachoTextGray;
                if (!(cell.AccessoryView is DisclosureAccessoryView)) {
                    cell.AccessoryView = new DisclosureAccessoryView ();
                }
                cell.SeparatorInset = TableView.SeparatorInset;
                return cell;
            }
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Row < Actions.Count ()) {
                var action = Actions.ActionAt (indexPath.Row);
                if (TableView.Editing) {
                    UpdateToolbarEnabled ();
                } else {
                    ShowMessage (action.Message);
                }
            } else {
                var index = indexPath.Row - Actions.Count ();
                var state = SubStates [index];
                ShowSubState (state);
            }
        }

        public override void RowDeselected (UITableView tableView, NSIndexPath indexPath)
        {
            if (TableView.Editing) {
                UpdateToolbarEnabled ();
            }
        }

        public override bool CanEditRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Row >= Actions.Count ()) {
                return false;
            }
            return true;
        }

        public override List<SwipeTableRowAction> ActionsForSwipingRightInRow (UITableView tableView, NSIndexPath indexPath)
        {
            return base.ActionsForSwipingRightInRow (tableView, indexPath);
        }

        public override List<SwipeTableRowAction> ActionsForSwipingLeftInRow (UITableView tableView, NSIndexPath indexPath)
        {
            return base.ActionsForSwipingLeftInRow (tableView, indexPath);
        }

        public override void WillBeginSwiping (UITableView tableView, NSIndexPath indexPath)
        {
            base.WillBeginSwiping (tableView, indexPath);
            UpdateNavigationItem ();
        }

        public override void DidEndSwiping (UITableView tableView, NSIndexPath indexPath)
        {
            base.DidEndSwiping (tableView, indexPath);
            UpdateNavigationItem ();
        }

        #endregion


        #region System Events

        void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd) {
                IsListeningForStatusInd = true;
                NcApplication.Instance.StatusIndEvent += StatusIndCallback;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent -= StatusIndCallback;
                IsListeningForStatusInd = false;
            }
        }

        void StatusIndCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            // TODO:
//            if (s.Account == null || (Messages != null && Messages.IsCompatibleWithAccount (s.Account))) {
//
//                bool isVisible = IsViewLoaded && View.Window != null;
//
//                switch (s.Status.SubKind) {
//                case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
//                    if (isVisible) {
//                        Reload ();
//                    }
//                    break;
//                case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
//                case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
//                case NcResult.SubKindEnum.Info_EmailMessageScoreUpdated:
//                case NcResult.SubKindEnum.Info_EmailMessageChanged:
//                case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
//                    if (isVisible) {
//                        UpdateVisibleRows ();
//                    }
//                    break;
//                case NcResult.SubKindEnum.Error_SyncFailed:
//                case NcResult.SubKindEnum.Info_SyncSucceeded:
//                    Messages.RefetchSyncTime ();
//                    break;
//                }
//            }
        }

        public void MessagesSyncDidComplete (MessagesSyncManager manager)
        {
            EndRefreshing ();
        }

        public void MessagesSyncDidTimeOut (MessagesSyncManager manager)
        {
            EndRefreshing ();
        }

        public override UIStatusBarStyle PreferredStatusBarStyle ()
        {
            return UIStatusBarStyle.LightContent;
        }

        #endregion

        #region Private Helpers

        string NameForState (McAction.ActionState state)
        {
            if (state == McAction.ActionState.Hot) {
                return "Hot";
            }
            if (state == McAction.ActionState.Open) {
                return "Open";
            }
            if (state == McAction.ActionState.Deferred) {
                return "Deferred";
            }
            if (state == McAction.ActionState.Completed) {
                return "Completed";
            }
            NcAssert.CaseError ();
            return null;
        }

        void ShowMessage (McEmailMessage message)
        {
            var messageViewController = new MessageViewController ();
            messageViewController.Message = message;
            NavigationController.PushViewController (messageViewController, true);
        }

        void ShowSubState (McAction.ActionState state)
        {
            var viewController = new ActionListViewController (state);
            NavigationController.PushViewController (viewController, animated: true);
        }

        void SwitchToAccount (McAccount account)
        {
            Account = account;
            // CancelSyncing ();
            if (TableView.Editing) {
                CancelEditingTable (animated: false);
            }
            if (SwipingIndexPath != null) {
                EndSwiping ();
            }
            SwitchAccountButton.SetAccountImage (account);
            Actions = new NachoActions (Account.Id, State);
            TableView.ReloadData ();  // to clear the table
            HasLoadedOnce = false;
            // Relying on ViewWillAppear to call Reload
        }

        void StartEditingTable ()
        {
            // TODO: adjust insets
            TableView.SetEditing(true, true);
            UpdateNavigationItem ();
            DeleteButton = new UIBarButtonItem ("Delete", UIBarButtonItemStyle.Plain, DeleteSelectedActions);
            ToolbarItems = new UIBarButtonItem[] {
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                DeleteButton,
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace)
            };
            UpdateToolbarEnabled ();
            NavigationController.SetToolbarHidden (false, true);
        }

        protected void CancelEditingTable (bool animated = true)
        {
            // TODO: adjust insets
            TableView.SetEditing (false, animated);
            UpdateNavigationItem ();
            NavigationController.SetToolbarHidden (true, true);
        }

        List<McAction> SelectedActions ()
        {
            var actions = new List<McAction> ();
            var indexPaths = TableView.IndexPathsForSelectedRows;
            if (indexPaths != null) {
                foreach (var indexPath in indexPaths) {
                    actions.Add (Actions.ActionAt (indexPath.Row));
                }
            }
            return actions;
        }

        void UpdateToolbarEnabled ()
        {
            var paths = TableView.IndexPathsForSelectedRows;
            var hasSelection = paths != null && paths.Length > 0;

            DeleteButton.Enabled = hasSelection;
        }

        protected virtual void UpdateNavigationItem ()
        {
            if (SwipingIndexPath != null) {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    DoneSwipingButton
                };
            } else if (IsViewLoaded && TableView.Editing) {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    CancelEditingButton
                };
            } else {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    EditTableButton
                };
            }
        }

        #endregion

        private class DisclosureAccessoryView : ImageAccessoryView
        {
            public DisclosureAccessoryView () : base ("gen-more-arrow")
            {
            }
        }

    }
}

