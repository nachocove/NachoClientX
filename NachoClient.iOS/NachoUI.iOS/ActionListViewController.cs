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
        UIBarButtonItem DoneEditingButton;

        UIBarButtonItem DeferButton;
        UIBarButtonItem DeleteButton;

        int NumberOfPreviewLines = 1;
        bool HasAppearedOnce = false;
        bool IsListeningForStatusInd = false;
        bool HasLoadedOnce = false;
        bool HasMadeEdits = false;

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
            DoneEditingButton = new UIBarButtonItem ("Done", UIBarButtonItemStyle.Plain, CancelEditingTable);
            DoneEditingButton.AccessibilityLabel = "Done Editing";
            DoneSwipingButton = new UIBarButtonItem ("Done", UIBarButtonItemStyle.Plain, EndSwiping);
            EditTableButton.AccessibilityLabel = "Done";

            UpdateNavigationItem ();
        }

        #endregion

        #region View Lifecycle

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.TintColor = A.Color_NachoGreen;
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
            DoneEditingButton.Clicked -= CancelEditingTable;
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
            var actions = SelectedActions ();
            NcTask.Run (() => {
                var messages = new List<McEmailMessage> (actions.Count);
                foreach (var action in actions) {
                    messages.Add (action.Message);
                }
                NcEmailArchiver.Delete (messages);
            }, "ActionListViewController_DeleteSelectedActions");
            CancelEditingTable ();
        }

        void DeleteAction (NSIndexPath indexPath)
        {
            DidEndSwiping (TableView, indexPath);
            var action = Actions.ActionAt (indexPath.Row);
            action.RemoveAndDeleteMessage ();
            NotifyActionsChanged (action);
        }

        void DemoteAction (NSIndexPath indexPath)
        {
            DidEndSwiping (TableView, indexPath);
            var action = Actions.ActionAt (indexPath.Row);
            action.RemoveButKeepMessage ();
            NotifyActionsChanged (action);
        }

        void MarkActionAsHot (NSIndexPath indexPath)
        {
            DidEndSwiping (TableView, indexPath);
            var action = Actions.ActionAt (indexPath.Row);
            if (!action.IsHot) {
                NcTask.Run (() => {
                    action.Hot ();
                    NotifyActionsChanged (action);
                }, "ActionListViewController_MarkActionAsHot", NcTask.ActionSerialScheduler);
            }
        }

        void MarkActionAsUnhot (NSIndexPath indexPath)
        {
            DidEndSwiping (TableView, indexPath);
            var action = Actions.ActionAt (indexPath.Row);
            if (action.IsHot) {
                NcTask.Run (() => {
                    action.Unhot ();
                    NotifyActionsChanged (action);
                }, "ActionListViewController_MarkActionAsUnhot", NcTask.ActionSerialScheduler);
            }
        }

        void DeferAction (NSIndexPath indexPath)
        {
            var action = Actions.ActionAt (indexPath.Row);
            var alertController = UIAlertController.Create ("", "Defer until...", UIAlertControllerStyle.ActionSheet);
            alertController.AddAction (UIAlertAction.Create ("An Hour From Now", UIAlertActionStyle.Default, (UIAlertAction alertAction) => { DeferAction(action, MessageDeferralType.OneHour); }));
            alertController.AddAction (UIAlertAction.Create ("Tonight", UIAlertActionStyle.Default, (UIAlertAction alertAction) => { DeferAction(action, MessageDeferralType.Tonight); }));
            alertController.AddAction (UIAlertAction.Create ("Tomorrow Morning", UIAlertActionStyle.Default, (UIAlertAction alertAction) => { DeferAction(action, MessageDeferralType.Tomorrow); }));
            alertController.AddAction (UIAlertAction.Create ("Monday Morning", UIAlertActionStyle.Default, (UIAlertAction alertAction) => { DeferAction(action, MessageDeferralType.NextWeek); }));
            alertController.AddAction (UIAlertAction.Create ("Saturday Morning", UIAlertActionStyle.Default, (UIAlertAction alertAction) => { DeferAction(action, MessageDeferralType.Weekend); }));
            alertController.AddAction (UIAlertAction.Create ("Other...", UIAlertActionStyle.Default, (UIAlertAction alertAction) => { DeferActionByEditing(action); }));
            alertController.AddAction (UIAlertAction.Create ("Cancel", UIAlertActionStyle.Cancel, null));
            PresentViewController (alertController, true, null);
        }

        void DeferAction (McAction action, MessageDeferralType type)
        {
            NcTask.Run (() => {
                action.Defer (type);
                NotifyActionsChanged (action);
            }, "ActionListViewController_DeferAction", NcTask.ActionSerialScheduler);
        }

        void DeferActionByEditing (McAction action)
        {
            var editedCopy = McAction.QueryById<McAction> (action.Id);
            editedCopy.State = McAction.ActionState.Deferred;
            EditAction (editedCopy);
        }

        void EditAction (NSIndexPath indexPath)
        {
            var action = Actions.ActionAt (indexPath.Row);
            EditAction (action);
        }

        void NotifyActionsChanged (McAction action)
        {
            var account = McAccount.QueryById<McAccount> (action.AccountId);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs() {
                Account = account,
                Status = NcResult.Info (NcResult.SubKindEnum.Info_ActionSetChanged)
            });
        }

        #endregion

        #region Reloading Data

        bool NeedsReload;
        bool IsReloading;

        void SetNeedsReload ()
        {
            NeedsReload = true;
            if (!IsReloading) {
                Reload ();
            }
        }

        void Reload ()
        {
            if (!IsReloading) {
                IsReloading = true;
                NeedsReload = false;
                Actions.BackgroundRefresh (HandleReloadResults);
            }
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
            IsReloading = false;
            if (NeedsReload) {
                Reload ();
            }
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
                cell.StrikesCompletedActions = State != McAction.ActionState.Completed;
                if (Actions.IncludesMultipleAccounts ()) {
                    cell.IndicatorColor = Util.ColorForAccount (action.AccountId);
                } else {
                    cell.IndicatorColor = null;
                }
                cell.SeparatorInset = TableView.SeparatorInset;
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

        public override bool CanMoveRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Row >= Actions.Count ()) {
                return false;
            }
            return State != McAction.ActionState.Completed;
        }

        public override NSIndexPath CustomizeMoveTarget (UITableView tableView, NSIndexPath sourceIndexPath, NSIndexPath proposedIndexPath)
        {
            if (proposedIndexPath.Row >= Actions.Count ()) {
                return NSIndexPath.FromRowSection (Actions.Count () - 1, proposedIndexPath.Section);
            }
            return proposedIndexPath;
        }

        public override void MoveRow (UITableView tableView, NSIndexPath sourceIndexPath, NSIndexPath destinationIndexPath)
        {
            if (sourceIndexPath.Row != destinationIndexPath.Row) {
                Actions.Move (sourceIndexPath.Row, destinationIndexPath.Row);
                var action = Actions.ActionAt (destinationIndexPath.Row);
                NcTask.Run (() => {
                    if (destinationIndexPath.Row == 0) {
                        action.MoveToFront ();
                    } else {
                        var previous = Actions.ActionAt (destinationIndexPath.Row - 1);
                        action.MoveAfterAction (previous);
                    }
                }, "ActionListViewController_MoveRow", NcTask.ActionSerialScheduler);
                HasMadeEdits = true;
                UpdateNavigationItem ();
            }
        }

        public override List<SwipeTableRowAction> ActionsForSwipingRightInRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Row < Actions.Count ()) {
                var action = Actions.ActionAt (indexPath.Row);
                var actions = new List<SwipeTableRowAction> ();
                if (!action.IsCompleted) {
                    if (action.IsHot) {
                        actions.Add (new SwipeTableRowAction ("Not Hot", UIImage.FromBundle ("email-not-hot"), UIColor.FromRGB (0xE6, 0x59, 0x59), MarkActionAsUnhot));
                    } else {
                        actions.Add (new SwipeTableRowAction ("Hot", UIImage.FromBundle ("email-hot"), UIColor.FromRGB (0xE6, 0x59, 0x59), MarkActionAsHot));
                    }
                    if (action.IsDeferred) {
                        actions.Add (new SwipeTableRowAction ("Edit", UIImage.FromBundle ("gen-edit"), UIColor.FromRGB (0x01, 0xB2, 0xCD), EditAction));
                    } else {
                        actions.Add (new SwipeTableRowAction ("Defer", UIImage.FromBundle ("email-defer-swipe"), UIColor.FromRGB (0x01, 0xB2, 0xCD), DeferAction));
                    }
                }
                return actions;
            }
            return null;
        }

        public override List<SwipeTableRowAction> ActionsForSwipingLeftInRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Row < Actions.Count ()) {
                var action = Actions.ActionAt (indexPath.Row);
                var actions = new List<SwipeTableRowAction> ();
                actions.Add (new SwipeTableRowAction ("Delete", UIImage.FromBundle ("email-delete-swipe"), UIColor.FromRGB (0xd2, 0x47, 0x47), DeleteAction));
                if (!action.IsCompleted) {
                    actions.Add (new SwipeTableRowAction ("Not Action", UIImage.FromBundle ("email-not-action-swipe"), UIColor.FromRGB (0xF5, 0x98, 0x27), DemoteAction));
                }
                return actions;
            }
            return null;
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

            if (s.Account == null || (Actions != null && Account.ContainsAccount (s.Account.Id))) {
                
                switch (s.Status.SubKind) {
                case NcResult.SubKindEnum.Info_ActionSetChanged:
                    SetNeedsReload ();
                    break;
                }
            }
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

        void EditAction (McAction action)
        {
            if (action.IsNew) {
                action.IsNew = false;
                action.Update ();
            }
            var viewController = new ActionEditViewController ();
            viewController.Action = action;
            viewController.PresentOverViewController (this);
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
            HasMadeEdits = false;
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
                if (HasMadeEdits) {
                    NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                        DoneEditingButton
                    };
                } else {
                    NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                        CancelEditingButton
                    };
                }
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

