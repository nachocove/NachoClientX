﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using UIKit;
using CoreGraphics;
using Foundation;
using NachoPlatform;

namespace NachoClient.iOS
{

    public interface ActionEditViewDelegate
    {
        void ActionEditViewDidSave (ActionEditViewController viewController);
        void ActionEditViewDidDismiss (ActionEditViewController viewController);
    }

    public class ActionEditViewController : NachoTableViewController, EditableTextCellDelegate, ThemeAdopter
    {

        #region Properties

        const string StateCellIdentifier = "StateCellIdentifier";
        const string DeferCellIdentifier = "DeferCellIdentifier";

        public ActionEditViewDelegate Delegate;
        public McAction Action;

        UIBarButtonItem SaveButton;
        UIBarButtonItem CloseButton;

        int NameSection = 0;
        int StateSection = 1;
        int DeferSection = 2;

        int NameRowTitle = 0;
        int NameRowDescription = 1;
        int NameRowDue = 2;

        bool HasAppearedOnce = false;

        ActionTitleCell TitleCell;
        EditableTextCell DescriptionCell;
        SwipeTableViewCell DueDateCell;

        DatePickerView DatePicker;

        class StateModel
        {
            public McAction.ActionState State;
            public string NameKey;
        }

        class DeferModel
        {
            public MessageDeferralType Type;
            public string NameKey;
        }

        static StateModel [] States = new StateModel [] {
            new StateModel(){ State = McAction.ActionState.Hot, NameKey = "Hot (action state)" },
            new StateModel(){ State = McAction.ActionState.Open, NameKey = "Normal (action state)" },
            new StateModel(){ State = McAction.ActionState.Deferred, NameKey = "Deferred (action state)" },
        };

        static DeferModel [] DeferralsIfNoDueDate = new DeferModel [] {
            new DeferModel(){ Type = MessageDeferralType.OneHour, NameKey = "An Hour From Now (action defer)"},
            new DeferModel(){ Type = MessageDeferralType.Tonight, NameKey = "Tonight (action defer)" },
            new DeferModel(){ Type = MessageDeferralType.Tomorrow, NameKey = "Tomorrow Morning (action defer)" },
            new DeferModel(){ Type = MessageDeferralType.NextWeek, NameKey = "Monday Morning (action defer)" },
            new DeferModel(){ Type = MessageDeferralType.Weekend, NameKey = "Saturday Morning (action defer)" },
            new DeferModel(){ Type = MessageDeferralType.Custom, NameKey = "Pick a Date (action defer)" }
        };

        static DeferModel [] DeferralsIfDueDate = new DeferModel [] {
            DeferralsIfNoDueDate[0],
            DeferralsIfNoDueDate[1],
            DeferralsIfNoDueDate[2],
            DeferralsIfNoDueDate[3],
            DeferralsIfNoDueDate[4],
            new DeferModel(){ Type = MessageDeferralType.DueDate, NameKey = "Due Date (action defer)" },
            DeferralsIfNoDueDate[5]
        };

        DeferModel [] Deferrals;

        #endregion

        #region Constructors

        public ActionEditViewController () : base (UITableViewStyle.Grouped)
        {
            AutomaticallyAdjustsScrollViewInsets = false;
            SaveButton = new UIBarButtonItem (NSBundle.MainBundle.LocalizedString ("Save", ""), UIBarButtonItemStyle.Plain, SaveButtonPressed);
            SaveButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Save", "");
            CloseButton = new NcUIBarButtonItem (UIImage.FromBundle ("icn-close"), UIBarButtonItemStyle.Plain, Close);
            CloseButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Close", "");
            NavigationItem.RightBarButtonItem = SaveButton;
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
            Deferrals = DeferralsIfNoDueDate;
        }

        #endregion

        #region Theme

        Theme adoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (theme != adoptedTheme) {
                adoptedTheme = theme;
                TableView.BackgroundColor = theme.TableViewGroupedBackgroundColor;
                TableView.TintColor = theme.TableViewTintColor;
                TitleCell.AdoptTheme (theme);
                DescriptionCell.TextView.Font = theme.DefaultFont.WithSize (14.0f);
                DescriptionCell.TextView.TextColor = theme.TableViewCellDetailLabelTextColor;
                DueDateCell.TextLabel.Font = theme.DefaultFont.WithSize (14.0f);
                ApplyThemeToHeader (_StateHeader);
                ApplyThemeToHeader (_DeferHeader);
            }
        }

        #endregion

        #region View Lifecycle

        public void PresentOverViewController (UIViewController presentingViewController)
        {
            NavigationItem.LeftBarButtonItem = CloseButton;
            var navController = new UINavigationController (this);
            presentingViewController.PresentViewController (navController, true, null);
        }

        public override void LoadView ()
        {
            base.LoadView ();
            TitleCell = new ActionTitleCell ();
            TitleCell.Placeholder = NSBundle.MainBundle.LocalizedString ("Summary", ""); ;
            TitleCell.Delegate = this;
            TitleCell.SeparatorInset = new UIEdgeInsets (0.0f, 44.0f, 0.0f, 0.0f);
            TitleCell.CheckboxView.Changed = CheckboxChanged;
            DescriptionCell = new EditableTextCell ();
            DescriptionCell.Placeholder = NSBundle.MainBundle.LocalizedString ("Notes", "");
            DescriptionCell.Delegate = this;
            DescriptionCell.SeparatorInset = TitleCell.SeparatorInset;

            TitleCell.TextView.ReturnKeyType = UIReturnKeyType.Next;
            TitleCell.FollowingResponder = DescriptionCell.TextView;

            DueDateCell = new SwipeTableViewCell ();
            DueDateCell.SeparatorInset = TitleCell.SeparatorInset;

            TableView.RegisterClassForCellReuse (typeof (StateCell), StateCellIdentifier);
            TableView.RegisterClassForCellReuse (typeof (NameValueCell), DeferCellIdentifier);
        }

        public override void ViewDidLoad ()
        {
            UpdateDeferals ();
            base.ViewDidLoad ();
            AdoptTheme (Theme.Active);
            TitleCell.TextView.Text = Action.Title;
            TitleCell.UpdatePlaceholderVisible ();
            TitleCell.CheckboxView.IsChecked = Action.IsCompleted;
            DescriptionCell.TextView.Text = Action.Description;
            DescriptionCell.UpdatePlaceholderVisible ();
            if (Action.Id == 0) {
                NavigationItem.Title = NSBundle.MainBundle.LocalizedString ("Create Action", "Title for action edit view when creating");
            } else {
                NavigationItem.Title = NSBundle.MainBundle.LocalizedString ("Edit Action", "Title for action edit view when modifying");
            }
            UpdateSaveEnabled ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (!HasAppearedOnce && Action.Id == 0) {
                TitleCell.TextView.BecomeFirstResponder ();
                TitleCell.TextView.SelectAll (null);
            }
            HasAppearedOnce = true;
            AdoptTheme (Theme.Active);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
        }

        public override void Cleanup ()
        {
            // Cleanup nav bar
            SaveButton.Clicked -= SaveButtonPressed;

            TitleCell.CheckboxView.Changed = null;
            TitleCell.Delegate = null;
            TitleCell.CheckboxView.Cleanup ();
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

        void CheckboxChanged (bool isChecked)
        {
            if (isChecked) {
                Action.State = McAction.ActionState.Completed;
                Action.CompletedDate = DateTime.UtcNow;
            } else {
                Action.State = McAction.ActionState.Open;
                Action.CompletedDate = default (DateTime);
            }
            TableView.ReloadSections (NSIndexSet.FromIndex (StateSection), UITableViewRowAnimation.None);
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
            if (indexPath.Section == NameSection) {
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
                    UpdateCheckboxTint ();
                    return TitleCell;
                } else if (indexPath.Row == NameRowDescription) {
                    return DescriptionCell;
                } else if (indexPath.Row == NameRowDue) {
                    UpdateDueDateCell ();
                    return DueDateCell;
                }
            } else if (indexPath.Section == StateSection) {
                var cell = tableView.DequeueReusableCell (StateCellIdentifier) as StateCell;
                var stateModel = States [indexPath.Row];
                cell.TextLabel.Text = NSBundle.MainBundle.LocalizedString (stateModel.NameKey, "");
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
                cell.TextLabel.Text = NSBundle.MainBundle.LocalizedString (deferModel.NameKey, "");
                if (deferModel.Type == MessageDeferralType.Custom) {
                    if (Action.DeferralType == MessageDeferralType.Custom && Action.DeferUntilDate != default (DateTime)) {
                        cell.ValueLabel.Text = DateTimeFormatter.Instance.AbbreviatedDateWithWeekdayAndYearExceptPresent (Action.DeferUntilDate);
                    } else {
                        cell.ValueLabel.Text = "";
                    }
                } else if (deferModel.Type == MessageDeferralType.DueDate) {
                    cell.ValueLabel.Text = DateTimeFormatter.Instance.AbbreviatedDateWithWeekdayAndYearExceptPresent (Action.DueDate);
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

        public override void WillDisplay (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            base.WillDisplay (tableView, cell, indexPath);
            var themed = cell as ThemeAdopter;
            if (themed != null && adoptedTheme != null) {
                themed.AdoptTheme (adoptedTheme);
            }
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
                    ShowDatePicker (Action.DueDate, (DateTime date) => {
                        if (date == default (DateTime) && Action.DeferralType == MessageDeferralType.DueDate) {
                            Action.DeferralType = MessageDeferralType.Custom;
                        }
                        Action.DueDate = date;
                        Action.DueDateIncludesTime = false;
                        UpdateDueDateCell ();
                        UpdateDeferals ();
                        if (Action.IsDeferred) {
                            TableView.ReloadSections (NSIndexSet.FromIndex (DeferSection), UITableViewRowAnimation.None);
                        }
                    });
                    TableView.DeselectRow (indexPath, true);
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
                    Action.DeferUntilDate = default (DateTime);
                }
                UpdateSectionCheckmark (indexPath);
                tableView.DeselectRow (indexPath, true);
                UpdateCheckboxTint ();
            } else if (indexPath.Section == DeferSection) {
                View.EndEditing (false);
                var deferModel = Deferrals [indexPath.Row];
                if (deferModel.Type == MessageDeferralType.Custom) {
                    ShowDatePicker (Action.DeferUntilDate, (DateTime date) => {
                        if (date == default (DateTime)) {
                            Action.DeferralType = MessageDeferralType.None;
                        } else {
                            Action.DeferralType = MessageDeferralType.Custom;
                        }
                        Action.DeferUntilDate = date;
                        UpdateSectionCheckmark (NSIndexPath.FromRowSection (Deferrals.Length, indexPath.Section));
                        TableView.ReloadRows (new NSIndexPath [] { indexPath }, UITableViewRowAnimation.None);
                    });
                } else if (Action.DeferralType == deferModel.Type) {
                    Action.DeferralType = MessageDeferralType.None;
                    Action.DeferUntilDate = default (DateTime);
                    UpdateSectionCheckmark (NSIndexPath.FromRowSection (Deferrals.Length, indexPath.Section));
                } else {
                    bool wasCustom = Action.DeferralType == MessageDeferralType.Custom;
                    Action.DeferralType = deferModel.Type;
                    var result = NachoCore.Brain.NcMessageDeferral.ComputeDeferral (DateTime.UtcNow, Action.DeferralType, Action.DueDate);
                    if (result.isOK ()) {
                        Action.DeferUntilDate = result.GetValue<DateTime> ();
                    }
                    UpdateSectionCheckmark (indexPath);
                    if (wasCustom) {
                        UpdateCustomDeferCell ();
                    }
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
                if (isNew) {
                    var account = McAccount.QueryById<McAccount> (Action.AccountId);
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                        Account = account,
                        Status = NcResult.Info (NcResult.SubKindEnum.Info_ActionSetChanged)
                    });
                }
                NachoPlatform.InvokeOnUIThread.Instance.Invoke (FinishSave);
                ActionsHelper.Instance.ScheduleNextUndeferCheck ();
            }, "ActionEditViewController_Save", NcTask.ActionSerialScheduler);
        }

        void FinishSave ()
        {
            if (Delegate != null) {
                Delegate.ActionEditViewDidSave (this);
            }
            Dismiss ();
        }

        void Dismiss ()
        {
            View.EndEditing (true);
            if (NavigationController.ViewControllers [0] == this) {
                DismissViewController (true, () => {
                    if (Delegate != null) {
                        Delegate.ActionEditViewDidDismiss (this);
                    }
                });
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
            if (Action.DueDate == default (DateTime)) {
                Deferrals = DeferralsIfNoDueDate;
            } else {
                Deferrals = DeferralsIfDueDate;
            }
        }

        void UpdateCheckboxTint ()
        {
            if (Action.State == McAction.ActionState.Hot) {
                TitleCell.CheckboxView.TintColor = adoptedTheme.ActionCheckboxColorHot;
            } else {
                TitleCell.CheckboxView.TintColor = adoptedTheme.TableViewCellMainLabelTextColor;
            }
        }

        void ShowDatePicker (DateTime preselectedDate, Action<DateTime> picked)
        {
            NavigationItem.RightBarButtonItem = null;
            View.EndEditing (true);
            DatePicker = new DatePickerView (NavigationController.View.Bounds);
            DatePicker.AdoptTheme (adoptedTheme);
            if (preselectedDate != default (DateTime)) {
                DatePicker.Date = preselectedDate;
            }
            NavigationController.View.AddSubview (DatePicker);
            DatePicker.LayoutIfNeeded ();
            DatePicker.Picked = (DateTime date) => {
                if (date != default (DateTime)) {
                    date = new DateTime (date.Year, date.Month, date.Day, 8, 0, 0, DateTimeKind.Local).ToUniversalTime ();
                }
                picked (date);
                HideDueDatePicker ();
            };
            DatePicker.Canceled = () => {
                HideDueDatePicker ();
            };
            DatePicker.Present ();
        }

        void HideDueDatePicker ()
        {
            DatePicker.Dismiss (() => {
                DatePicker.RemoveFromSuperview ();
                DatePicker.Picked = null;
                DatePicker.Cleanup ();
                DatePicker = null;
                NavigationItem.RightBarButtonItem = SaveButton;
            });
        }

        void UpdateDueDateCell ()
        {
            if (Action.DueDate == default (DateTime)) {
                DueDateCell.TextLabel.Text = NSBundle.MainBundle.LocalizedString ("Set a Due Date (action edit)", "Detail text for action due date when nothing is set");
                DueDateCell.TextLabel.TextColor = DueDateCell.ContentView.BackgroundColor.ColorDarkenedByAmount (0.15f);
            } else {
                DueDateCell.TextLabel.Text = String.Format (NSBundle.MainBundle.LocalizedString ("Due on {0} (action edit)", "Detail text for action due date when a date is set"), DateTimeFormatter.Instance.AbbreviatedDateWithWeekdayAndYearExceptPresent (Action.DueDate));
                DueDateCell.TextLabel.TextColor = adoptedTheme.TableViewCellDetailLabelTextColor;
            }
        }

        void UpdateCustomDeferCell ()
        {
            foreach (var indexPath in TableView.IndexPathsForVisibleRows) {
                if (indexPath.Section == DeferSection) {
                    var deferModel = Deferrals [indexPath.Row];
                    if (deferModel.Type == MessageDeferralType.Custom) {
                        TableView.ReloadRows (new NSIndexPath [] { indexPath }, UITableViewRowAnimation.None);
                        break;
                    }
                }
            }
        }

        #endregion

        #region Headers

        void ApplyThemeToHeader (InsetLabelView header)
        {
            if (header != null) {
                header.Label.Font = adoptedTheme.DefaultFont.WithSize (14.0f);
                header.Label.TextColor = adoptedTheme.TableSectionHeaderTextColor;
            }
        }

        private InsetLabelView _StateHeader;
        private InsetLabelView StateHeader {
            get {
                if (_StateHeader == null) {
                    _StateHeader = new InsetLabelView ();
                    _StateHeader.LabelInsets = new UIEdgeInsets (5.0f, GroupedCellInset + 6.0f, 5.0f, GroupedCellInset);
                    _StateHeader.Label.Text = NSBundle.MainBundle.LocalizedString ("Priority (action edit)", "");
                    _StateHeader.Frame = new CGRect (0.0f, 0.0f, 100.0f, 20.0f);
                    if (adoptedTheme != null) {
                        ApplyThemeToHeader (_StateHeader);
                    }
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
                    _DeferHeader.Label.Text = NSBundle.MainBundle.LocalizedString ("Defer Until (action edit)", "");
                    _DeferHeader.Frame = new CGRect (0.0f, 0.0f, 100.0f, 20.0f);
                    if (adoptedTheme != null) {
                        ApplyThemeToHeader (_DeferHeader);
                    }
                }
                return _DeferHeader;
            }
        }

        #endregion

        #region Cells

        private class CheckmarkAccessoryView : ImageAccessoryView
        {
            public CheckmarkAccessoryView () : base ("checkmark-accessory")
            {
            }
        }

        class ActionTitleCell : EditableTextCell, ThemeAdopter
        {

            public readonly ActionCheckboxView CheckboxView;

            public ActionTitleCell () : base ()
            {
                CheckboxView = new ActionCheckboxView (44.0f, checkboxSize: 20.0f);
                ContentView.AddSubview (CheckboxView);

                AllowsNewlines = false;
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                CheckboxView.Center = new CGPoint (SeparatorInset.Left / 2.0f, 22.0f);
            }

            public void AdoptTheme (Theme theme)
            {
                TextView.Font = theme.BoldDefaultFont.WithSize (17.0f);
                TextView.TextColor = theme.DefaultTextColor;
            }

        }

        class StateCell : SwipeTableViewCell, ThemeAdopter
        {

            public StateCell (IntPtr ptr) : base (ptr)
            {
            }

            public void AdoptTheme (Theme theme)
            {
                TextLabel.Font = theme.DefaultFont.WithSize (14.0f);
                TextLabel.TextColor = theme.TableViewCellMainLabelTextColor;
            }
        }

        #endregion
    }
}

