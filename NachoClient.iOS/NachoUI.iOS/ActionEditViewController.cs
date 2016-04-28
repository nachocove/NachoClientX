//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using UIKit;
using CoreGraphics;
using Foundation;

namespace NachoClient.iOS
{
    public class ActionEditViewController : NachoTableViewController, EditableTextCellDelegate
    {

        #region Properties

        const string StateCellIdentifier = "StateCellIdentifier";
        const string DeferCellIdentifier = "DeferCellIdentifier";

        public McAction Action;

        UIBarButtonItem SaveButton;
        UIBarButtonItem CloseButton;

        int NameSection = 0;
        int StateSection = 1;
        int DeferSection = 2;

        int NameRowTitle = 0;
        int NameRowDescription = 1;
        int NameRowDue = 2;

        ActionTitleCell TitleCell;
        EditableTextCell DescriptionCell;
        SwipeTableViewCell DueDateCell;

        class StateModel {
            public McAction.ActionState State;
            public string Name;
        }

        class DeferModel {
            public MessageDeferralType Type;
            public string Name;
        }

        static StateModel[] States = new StateModel[] {
            new StateModel(){ State = McAction.ActionState.Hot, Name = "Hot" },
            new StateModel(){ State = McAction.ActionState.Open, Name = "Normal" },
            new StateModel(){ State = McAction.ActionState.Deferred, Name = "Deferred" },
        };

        static DeferModel[] DeferralsIfNoDueDate = new DeferModel[] {
            new DeferModel(){ Type = MessageDeferralType.OneHour, Name = "An Hour From Now"},
            new DeferModel(){ Type = MessageDeferralType.Tonight, Name = "Tonight" },
            new DeferModel(){ Type = MessageDeferralType.Tomorrow, Name = "Tomorrow Morning" },
            new DeferModel(){ Type = MessageDeferralType.NextWeek, Name = "Monday Morning" },
            new DeferModel(){ Type = MessageDeferralType.Weekend, Name = "Saturday Morning" },
            new DeferModel(){ Type = MessageDeferralType.Custom, Name = "Pick a Date" }
        };

        static DeferModel[] DeferralsIfDueDate = new DeferModel[] {
            DeferralsIfNoDueDate[0],
            DeferralsIfNoDueDate[1],
            DeferralsIfNoDueDate[2],
            DeferralsIfNoDueDate[3],
            DeferralsIfNoDueDate[4],
            new DeferModel(){ Type = MessageDeferralType.DueDate, Name = "Due Date" },
            DeferralsIfNoDueDate[5]
        };

        DeferModel[] Deferrals;

        #endregion

        #region Constructors

        public ActionEditViewController () : base (UITableViewStyle.Grouped)
        {
            AutomaticallyAdjustsScrollViewInsets = false;
            SaveButton = new UIBarButtonItem ("Save", UIBarButtonItemStyle.Plain, SaveButtonPressed);
            SaveButton.AccessibilityLabel = "Save";
            CloseButton = new NcUIBarButtonItem (UIImage.FromBundle ("icn-close"), UIBarButtonItemStyle.Plain, Close);
            CloseButton.AccessibilityLabel = "Close";
            NavigationItem.RightBarButtonItem = SaveButton;
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
            Deferrals = DeferralsIfNoDueDate;
        }

        #endregion

        #region View Lifecycle

        public void PresentOverViewController (UIViewController presentingViewController)
        {
            NavigationItem.LeftBarButtonItem = CloseButton;
            var navController = new UINavigationController (this);
            Util.ConfigureNavBar (false, navController);
            presentingViewController.PresentViewController (navController, true, null);
        }

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.BackgroundColor = A.Color_NachoBackgroundGray;
            TitleCell = new ActionTitleCell ();
            TitleCell.Placeholder = "Summary";
            TitleCell.Delegate = this;
            DescriptionCell = new EditableTextCell ();
            DescriptionCell.TextView.Font = A.Font_AvenirNextRegular14;
            DescriptionCell.TextView.TextColor = A.Color_NachoTextGray;
            DescriptionCell.Placeholder = "Notes";
            DescriptionCell.Delegate = this;

            TitleCell.TextView.ReturnKeyType = UIReturnKeyType.Next;
            TitleCell.FollowingResponder = DescriptionCell.TextView;

            DueDateCell = new SwipeTableViewCell ();
            DueDateCell.TextLabel.Font = A.Font_AvenirNextRegular14;

            TableView.RegisterClassForCellReuse (typeof(SwipeTableViewCell), StateCellIdentifier);
            TableView.RegisterClassForCellReuse (typeof(NameValueCell), DeferCellIdentifier);
        }

        public override void ViewDidLoad ()
        {
            UpdateDeferals ();
            base.ViewDidLoad ();
            TitleCell.TextView.Text = Action.Title;
            TitleCell.UpdatePlaceholderVisible ();
            DescriptionCell.TextView.Text = Action.Description;
            DescriptionCell.UpdatePlaceholderVisible ();
            if (Action.Id == 0){
                NavigationItem.Title = "Create Action";
            }else{
                NavigationItem.Title = "Edit Action";
            }
            UpdateSaveEnabled ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            TitleCell.TextView.BecomeFirstResponder ();
            TitleCell.TextView.SelectAll (null);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
        }

        protected override void Cleanup ()
        {
            // Cleanup nav bar
            SaveButton.Clicked -= SaveButtonPressed;

            TitleCell.Delegate = null;
            DescriptionCell.Delegate = null;

            base.Cleanup ();
        }

        #endregion

        #region User Actions

        void SaveButtonPressed (object sender, EventArgs e)
        {
            Save ();
        }

        void Close (object sender, EventArgs e)
        {
            Dismiss ();
        }

        public void EditableCellDidChange (EditableTextCell cell)
        {
            if (cell == TitleCell) {
                Action.Title = cell.TextView.Text.Trim ();
                UpdateSaveEnabled ();
            } else if (cell == DescriptionCell) {
                Action.Description = cell.TextView.Text.Trim ();
            }
        }

        #endregion

        #region Table Delegate & Data Source

        public override nint NumberOfSections (UITableView tableView)
        {
            if (Action.State == McAction.ActionState.Deferred) {
                return 3;
            }
            return 2;
        }

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            if (section == NameSection) {
                return 3;
            }
            if (section == StateSection) {
                return States.Length;
            }
            if (section == DeferSection) {
                return Deferrals.Length;
            }
            return 0;
        }

        public override nfloat GetHeightForHeader (UITableView tableView, nint section)
        {
            if (section == StateSection) {
                return StateHeader.PreferredHeight;
            } else if (section == DeferSection) {
                return DeferHeader.PreferredHeight;
            }
            return 0.0f;
        }

        public override UIView GetViewForHeader (UITableView tableView, nint section)
        {
            if (section == StateSection) {
                return StateHeader;
            } else if (section == DeferSection) {
                return DeferHeader;
            }
            return null;
        }

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == NameSection){
                if (indexPath.Row == NameRowTitle) {
                    TitleCell.PrepareForWidth (TableView.Bounds.Width);
                    return TitleCell.Height;
                } else if (indexPath.Row == NameRowDescription) {
                    DescriptionCell.PrepareForWidth (TableView.Bounds.Width);
                    return DescriptionCell.Height;
                }
            }
            return 44.0f;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == NameSection) {
                if (indexPath.Row == NameRowTitle) {
                    return TitleCell;
                } else if (indexPath.Row == NameRowDescription) {
                    return DescriptionCell;
                } else if (indexPath.Row == NameRowDue) {
                    if (Action.DueDate == default(DateTime)) {
                        DueDateCell.TextLabel.Text = "Set a Due Date";
                        DueDateCell.TextLabel.TextColor = DueDateCell.ContentView.BackgroundColor.ColorDarkenedByAmount (0.15f);
                    } else {
                        DueDateCell.TextLabel.Text = String.Format ("Due on {0}", Pretty.MediumFullDate (Action.DueDate));
                        DueDateCell.TextLabel.TextColor = A.Color_NachoTextGray;
                    }
                    return DueDateCell;
                }
            } else if (indexPath.Section == StateSection) {
                var cell = tableView.DequeueReusableCell (StateCellIdentifier) as SwipeTableViewCell;
                var stateModel = States [indexPath.Row];
                cell.TextLabel.Font = A.Font_AvenirNextRegular14;
                cell.TextLabel.TextColor = A.Color_NachoGreen;
                cell.TextLabel.Text = stateModel.Name;
                if (Action.State == stateModel.State) {
                    if (!(cell.AccessoryView is CheckmarkAccessoryView)) {
                        cell.AccessoryView = new CheckmarkAccessoryView ();
                    }
                } else {
                    if (cell.AccessoryView != null) {
                        cell.AccessoryView = null;
                    }
                }
                return cell;
            } else if (indexPath.Section == DeferSection) {
                var cell = tableView.DequeueReusableCell (DeferCellIdentifier) as NameValueCell;
                var deferModel = Deferrals [indexPath.Row];
                cell.TextLabel.Font = A.Font_AvenirNextRegular14;
                cell.TextLabel.TextColor = A.Color_NachoGreen;
                cell.TextLabel.Text = deferModel.Name;
                if (deferModel.Type == MessageDeferralType.Custom) {
                    if (Action.DeferUntilDate != default (DateTime)) {
                        cell.ValueLabel.Text = Pretty.MediumFullDate (Action.DeferUntilDate);
                    } else {
                        cell.ValueLabel.Text = "";
                    }
                } else if (deferModel.Type == MessageDeferralType.DueDate){
                    cell.ValueLabel.Text = Pretty.MediumFullDate (Action.DueDate);
                } else {
                    cell.ValueLabel.Text = "";
                }
                if (Action.DeferralType == deferModel.Type) {
                    if (!(cell.AccessoryView is CheckmarkAccessoryView)) {
                        cell.AccessoryView = new CheckmarkAccessoryView ();
                    }
                } else {
                    if (cell.AccessoryView != null) {
                        cell.AccessoryView = null;
                    }
                }
                return cell;
            }
            return null;
        }

        public override bool ShouldHighlightRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == NameSection) {
                if (indexPath.Row == NameRowTitle || indexPath.Row == NameRowDescription) {
                    return false;
                }
            }
            return base.ShouldHighlightRow (tableView, indexPath);
        }

        public override NSIndexPath WillSelectRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == NameSection) {
                if (indexPath.Row == NameRowTitle || indexPath.Row == NameRowDescription) {
                    return null;
                }
            }
            return base.WillSelectRow (tableView, indexPath);
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == NameSection) {
                if (indexPath.Row == NameRowDue) {
                    // TODO: show date picker
                }
            } else if (indexPath.Section == StateSection) {
                View.EndEditing (false);
                var stateModel = States [indexPath.Row];
                var previousState = Action.State;
                Action.State = stateModel.State;
                if (previousState != McAction.ActionState.Deferred && Action.State == McAction.ActionState.Deferred) {
                    tableView.InsertSections (NSIndexSet.FromIndex (DeferSection), UITableViewRowAnimation.Fade);
                } else if (previousState == McAction.ActionState.Deferred && Action.State != McAction.ActionState.Deferred) {
                    tableView.DeleteSections (NSIndexSet.FromIndex (DeferSection), UITableViewRowAnimation.Fade);
                    Action.DeferralType = MessageDeferralType.None;
                    Action.DeferUntilDate = default(DateTime);
                }
                UpdateSectionCheckmark (indexPath);
                tableView.DeselectRow (indexPath, true);
            } else if (indexPath.Section == DeferSection) {
                View.EndEditing (false);
                var deferModel = Deferrals [indexPath.Row];
                if (Action.DeferralType == deferModel.Type) {
                    Action.DeferralType = MessageDeferralType.None;
                    Action.DeferUntilDate = default(DateTime);
                    UpdateSectionCheckmark (NSIndexPath.FromRowSection (Deferrals.Length, indexPath.Section));
                } else if (deferModel.Type == MessageDeferralType.Custom) {
                    // TODO: show date picker
                } else {
                    Action.DeferralType = deferModel.Type;
                    var result = NachoCore.Brain.NcMessageDeferral.ComputeDeferral (DateTime.UtcNow, Action.DeferralType, Action.DueDate);
                    if (result.isOK ()) {
                        Action.DeferUntilDate = result.GetValue<DateTime> ();
                    }
                    UpdateSectionCheckmark (indexPath);
                }
                tableView.DeselectRow (indexPath, true);
            }
        }

        #endregion

        #region Private Helpers

        void UpdateSaveEnabled ()
        {
            var canSave = !String.IsNullOrWhiteSpace (Action.Title);
            SaveButton.Enabled = canSave;
        }

        void Save ()
        {
            SaveButton.Enabled = false;
            // TODO run in a serial task queue so orders won't collide
            NcTask.Run (() => {
                var isNew = Action.Id == 0;
                NcModel.Instance.RunInTransaction (() => {
                    if (Action.Id == 0) {
                        Action.Insert ();
                        Action.MoveToFront ();
                        Action.Message.UpdateWithOCApply<McEmailMessage> ((McAbstrObject record) => {
                            var message = record as McEmailMessage;
                            message.IsAction = true;
                            return true;
                        });
                    } else {
                        var actionBeforeChanges = McAction.QueryById<McAction> (Action.Id);
                        Action.Update ();
                        if (actionBeforeChanges.State != Action.State) {
                            Action.MoveToFront ();
                        }
                    }
                    Action.UpdateMessageFlag ();
                });
                if (isNew){
                    var account = McAccount.QueryById<McAccount> (Action.AccountId);
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs() {
                        Account = account,
                        Status = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged)
                    });
                }
                NachoPlatform.InvokeOnUIThread.Instance.Invoke (FinishSave);
            }, "ActionEditViewController_Save");
        }

        void FinishSave ()
        {
            Dismiss ();
        }

        void Dismiss ()
        {
            View.EndEditing (true);
            if (NavigationController.ViewControllers [0] == this) {
                DismissViewController (true, null);
            } else {
                NavigationController.PopViewController (true);
            }
        }

        void UpdateSectionCheckmark (NSIndexPath selectedIndexPath)
        {
            SwipeTableViewCell cell;
            foreach (var indexPath in TableView.IndexPathsForVisibleRows) {
                cell = TableView.CellAt (indexPath) as SwipeTableViewCell;
                if (indexPath.Section == selectedIndexPath.Section) {
                    if (indexPath.Equals (selectedIndexPath)) {
                        if (cell.AccessoryView == null) {
                            cell.AccessoryView = new CheckmarkAccessoryView ();
                        }
                    } else {
                        if (cell.AccessoryView != null) {
                            cell.AccessoryView = null;
                        }
                    }
                }
            }
        }

        void UpdateDeferals ()
        {
            if (Action.DueDate == default(DateTime)) {
                Deferrals = DeferralsIfNoDueDate;
            } else {
                Deferrals = DeferralsIfDueDate;
            }
        }

        #endregion

        #region Headers

        private InsetLabelView _StateHeader;
        private InsetLabelView StateHeader {
            get {
                if (_StateHeader == null) {
                    _StateHeader = new InsetLabelView ();
                    _StateHeader.LabelInsets = new UIEdgeInsets (5.0f, GroupedCellInset + 6.0f, 5.0f, GroupedCellInset);
                    _StateHeader.Label.Text = "Priority";
                    _StateHeader.Label.Font = A.Font_AvenirNextRegular14;
                    _StateHeader.Label.TextColor = TableView.BackgroundColor.ColorDarkenedByAmount (0.6f);
                    _StateHeader.Frame = new CGRect (0.0f, 0.0f, 100.0f, 20.0f);
                }
                return _StateHeader;
            }
        }

        private InsetLabelView _DeferHeader;
        private InsetLabelView DeferHeader {
            get {
                if (_DeferHeader == null) {
                    _DeferHeader = new InsetLabelView ();
                    _DeferHeader.LabelInsets = new UIEdgeInsets (5.0f, GroupedCellInset + 6.0f, 5.0f, GroupedCellInset);
                    _DeferHeader.Label.Text = "Defer Until";
                    _DeferHeader.Label.Font = A.Font_AvenirNextRegular14;
                    _DeferHeader.Label.TextColor = TableView.BackgroundColor.ColorDarkenedByAmount (0.6f);
                    _DeferHeader.Frame = new CGRect (0.0f, 0.0f, 100.0f, 20.0f);
                }
                return _DeferHeader;
            }
        }

        #endregion

        #region Cells

        private class CheckmarkAccessoryView : ImageAccessoryView
        {
            public CheckmarkAccessoryView () : base ("gen-checkbox-checked")
            {
            }
        }

        class ActionTitleCell : EditableTextCell
        {
            public ActionTitleCell () : base ()
            {
                AllowsNewlines = false;
                TextView.Font = A.Font_AvenirNextDemiBold17;
                TextView.TextColor = A.Color_NachoDarkText;
            }

        }

        #endregion
    }
}

