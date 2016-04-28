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
    public class ActionEditViewController : NachoTableViewController
    {

        #region Properties

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

        class StateModel {
            public McAction.ActionState State;
            public string Name;
        }

        class DeferModel {
            public string Name;
        }

        static StateModel[] States = new StateModel[] {
            new StateModel(){ State = McAction.ActionState.Hot, Name = "Hot" },
            new StateModel(){ State = McAction.ActionState.Open, Name = "Normal" },
            new StateModel(){ State = McAction.ActionState.Deferred, Name = "Deferred" },
        };

        static DeferModel[] Deferrals = new DeferModel[] {
            new DeferModel(){ Name = "One Hour"},
            new DeferModel(){ Name = "Tonight" },
            new DeferModel(){ Name = "Tomorrow Morning" },
            new DeferModel(){ Name = "Monday Morning" },
            new DeferModel(){ Name = "Pick a Date" }
            // On due date
            // Days before due date
        };

        #endregion

        #region Constructors

        public ActionEditViewController () : base (UITableViewStyle.Grouped)
        {
            SaveButton = new UIBarButtonItem ("Save", UIBarButtonItemStyle.Plain, SaveButtonPressed);
            SaveButton.AccessibilityLabel = "Save";
            CloseButton = new NcUIBarButtonItem (UIImage.FromBundle ("icn-close"), UIBarButtonItemStyle.Plain, Close);
            CloseButton.AccessibilityLabel = "Close";
            NavigationItem.RightBarButtonItem = SaveButton;
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
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
            DescriptionCell = new EditableTextCell ();
            DescriptionCell.TextView.Font = A.Font_AvenirNextRegular14;
            DescriptionCell.TextView.TextColor = A.Color_NachoTextGray;
            DescriptionCell.Placeholder = "Notes";

            TitleCell.TextView.ReturnKeyType = UIReturnKeyType.Next;
            TitleCell.FollowingResponder = DescriptionCell.TextView;
        }

        public override void ViewDidLoad ()
        {
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
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            TitleCell.TextView.BecomeFirstResponder ();
            TitleCell.TextView.SelectAll (null);
        }

        protected override void Cleanup ()
        {
            // Cleanup nav bar
            SaveButton.Clicked -= SaveButtonPressed;

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

        #endregion

        #region Table Delegate & Data Source

        public override nint NumberOfSections (UITableView tableView)
        {
            if (Action.State == McAction.ActionState.Deferred) {
                return 1;
            }
            return 1;
        }

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            if (section == NameSection) {
                return 2;
            }
            if (section == StateSection) {
                return States.Length;
            }
            if (section == DeferSection) {
                return Deferrals.Length;
            }
            return 0;
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
                }
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
                var stateModel = States [indexPath.Row];
                Action.State = stateModel.State;
            } else if (indexPath.Section == DeferSection) {
//                var deferral = Deferrals [indexPath.Row];
            }
        }

        #endregion

        #region Private Helpers

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

        #endregion

        #region Headers

        private InsetLabelView _StateHeader;
        private InsetLabelView StateHeader {
            get {
                if (_StateHeader == null) {
                    _StateHeader = new InsetLabelView ();
                    _StateHeader.LabelInsets = new UIEdgeInsets (20.0f, GroupedCellInset + 6.0f, 5.0f, GroupedCellInset);
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
                    _DeferHeader.LabelInsets = new UIEdgeInsets (20.0f, GroupedCellInset + 6.0f, 5.0f, GroupedCellInset);
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

