﻿﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using Foundation;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class ActionSheetViewController : NcUIViewController, IUITableViewDelegate, IUITableViewDataSource, ThemeAdopter, IUIViewControllerAnimatedTransitioning
    {

        private ActionSheetView ActionView;
        private List<ActionSheetItem> Items = new List<ActionSheetItem> ();
        private List<ActionSheetItem> ReplacementItems = new List<ActionSheetItem> ();
        private nfloat AnimationDuration = 0.3f;
        private const string ActionCellReuseIdentifier = "ActionCell";

        public ActionSheetViewController () : base ()
        {
            ModalPresentationStyle = UIModalPresentationStyle.Custom;
            TransitioningDelegate = new _TransitioningDelegate (this);
		}

		public override UIStatusBarStyle PreferredStatusBarStyle ()
		{
			return ParentViewController.PreferredStatusBarStyle ();
		}

		#region Presentation

		[Export ("animateTransition:")]
		public void AnimateTransition (IUIViewControllerContextTransitioning transitionContext)
        {
			if (IsBeingPresented) {
                View.Frame = transitionContext.ContainerView.Bounds;
				transitionContext.ContainerView.AddSubview (View);
				ActionView.ConfigureForDismissed ();
                ActionView.LayoutIfNeeded ();
            }
            UIView.AnimateNotify (TransitionDuration(transitionContext), 0.0f, UIViewAnimationOptions.CurveEaseInOut, () => {
                if (IsBeingPresented){
                    ActionView.ConfigureForPresented ();
                }else{
                    ActionView.ConfigureForDismissed ();
                }
                ActionView.LayoutIfNeeded ();
			}, (completed) => {
				if (IsBeingDismissed) {
					View.RemoveFromSuperview ();
				}
				transitionContext.CompleteTransition (completed);
			});
        }

		[Export ("transitionDuration:")]
		public double TransitionDuration (IUIViewControllerContextTransitioning transitionContext)
        {
            return 0.3;
        }

        class _TransitioningDelegate : UIViewControllerTransitioningDelegate
        {

            IUIViewControllerAnimatedTransitioning AnimatedTransitioning;

            public _TransitioningDelegate (IUIViewControllerAnimatedTransitioning animatedTransitioning)
            {
                AnimatedTransitioning = animatedTransitioning;
            }

            public override UIPresentationController GetPresentationControllerForPresentedViewController (UIViewController presentedViewController, UIViewController presentingViewController, UIViewController sourceViewController)
            {
                return new _PresentationController (presentedViewController, presentingViewController);
            }

            public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForDismissedController (UIViewController dismissed)
            {
                return AnimatedTransitioning;
            }

            public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForPresentedController (UIViewController presented, UIViewController presenting, UIViewController source)
            {
                return AnimatedTransitioning;
            }
        }

        class _PresentationController : UIPresentationController
        {

            public _PresentationController (UIViewController presented, UIViewController presenting) : base (presented, presenting)
            {
                if (presented.View.Superview != null){
                    presented.View.RemoveFromSuperview ();
                }
            }

            public override void ContainerViewWillLayoutSubviews ()
            {
                PresentedView.Frame = FrameOfPresentedViewInContainerView;
            }

            public override CGRect FrameOfPresentedViewInContainerView {
                get {
                    return ContainerView.Bounds;
                }
            }

            public override bool ShouldPresentInFullscreen {
                get {
                    return true;
                }
            }

            public override bool ShouldRemovePresentersView {
                get {
                    return false;
                }
            }
        }

        #endregion

        #region Managing Items

        bool IsReplacingItems;

        public void AddItem (ActionSheetItem item)
        {
            if (IsReplacingItems) {
                ReplacementItems.Add (item);
            }else{
                Items.Add (item);
            }
        }

        public void BeginReplacingItems ()
        {
            IsReplacingItems = true;
            ReplacementItems.Clear ();
        }

        public void EndReplacingItems ()
        {
            IsReplacingItems = false;
            var originalIndexPaths = new List<NSIndexPath> ();
            var replacementIndexPaths = new List<NSIndexPath> ();
            for (var i = 0; i < Items.Count; ++i){
                originalIndexPaths.Add (NSIndexPath.FromRowSection (i, 0));
            }
            for (var i = 0; i < ReplacementItems.Count; ++i){
                replacementIndexPaths.Add (NSIndexPath.FromRowSection (i, 0));
            }
            ActionView.TableView.BeginUpdates ();
            ActionView.TableView.DeleteRows (originalIndexPaths.ToArray (), UITableViewRowAnimation.Left);
            ActionView.TableView.InsertRows (replacementIndexPaths.ToArray (), UITableViewRowAnimation.Right);
            Items = ReplacementItems;
            ReplacementItems = new List<ActionSheetItem> ();
            ActionView.TableView.EndUpdates ();
            // TODO: animate new layout?
            ActionView.SetNeedsLayout ();
        }

        #endregion

        #region View Lifecycle

        public override void LoadView ()
        {
            View = ActionView = new ActionSheetView ();
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            ActionView.TableView.Delegate = this;
            ActionView.TableView.DataSource = this;
            ActionView.TableView.RegisterClassForCellReuse (typeof(ActionCell), ActionCellReuseIdentifier);
            ActionView.CancelButton.TouchUpInside += Cancel;
            ActionView.BackgroundView.AddGestureRecognizer (new UITapGestureRecognizer (() => { Dismiss (); }));
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            AdoptTheme (Theme.Active);
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            if (ShouldCleanupDuringDidDisappear){
                Cleanup ();
            }
        }

        private void Cleanup ()
        {
            ActionView.TableView.Delegate = null;
            ActionView.TableView.DataSource = null;
            ActionView.CancelButton.TouchUpInside -= Cancel;
            ActionView.BackgroundView.RemoveGestureRecognizer (ActionView.BackgroundView.GestureRecognizers[0]);
            TransitioningDelegate = null;
        }

        #endregion

        #region Theme

        Theme AdoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (theme != AdoptedTheme){
                AdoptedTheme = theme;
                ActionView.AdoptTheme (theme);
            }
        }

        #endregion

        #region User Actions

        void Cancel (object sender, EventArgs e)
        {
            Dismiss ();
        }

        void Dismiss ()
        {
            DismissViewController (animated: true, completionHandler: null);
        }

        #endregion

        #region Table DataSource & Delegate


        [Export ("numberOfSectionsInTableView:")]
        public nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        [Export ("tableView:numberOfRowsInSection:")]
		public nint RowsInSection (UITableView tableView, nint section)
		{
			return Items.Count;
		}

        [Export ("tableView:cellForRowAtIndexPath:")]
        public UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell (ActionCellReuseIdentifier, indexPath);
            var item = Items [indexPath.Row];
            if (item.AccessoryImageName != null){
                cell.AccessoryView = new ImageAccessoryView (item.AccessoryImageName);
                if (item.AccessoryAction != null){
                    cell.AccessoryView.AddGestureRecognizer (new UITapGestureRecognizer (() => {
                        item.AccessoryAction ();
                        if (item.DismissesSheet){
                            Dismiss ();
                        }
                    }));
                }
            }else{
                cell.AccessoryView = null;
            }
            cell.TextLabel.Text = item.Title;
            return cell;
        }

        [Export ("tableView:willDisplayCell:forRowAtIndexPath:")]
        public void WillDisplay (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            var themed = cell as ThemeAdopter;
            if (themed != null && AdoptedTheme != null) {
                themed.AdoptTheme (AdoptedTheme);
            }
        }

        [Export ("tableView:willSelectRowAtIndexPath:")]
        public NSIndexPath WillSelectRow (UITableView tableView, NSIndexPath indexPath)
        {
            return indexPath;
        }

        [Export ("tableView:shouldHighlightRowAtIndexPath:")]
        public bool ShouldHighlightRow (UITableView tableView, NSIndexPath rowIndexPath)
        {
            return true;
        }

        [Export ("tableView:didSelectRowAtIndexPath:")]
        public void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            var item = Items [indexPath.Row];
            item.Action ();
            if (item.DismissesSheet){
                Dismiss ();
            }
        }

        #endregion

        private class ActionSheetView : UIView, ThemeAdopter
        {

            public UIView BackgroundView { get; private set; }
            public UITableView TableView { get; private set; }
            public UIButton CancelButton { get; private set; }
            nfloat CancelTableSpacing = 8.0f;
            nfloat BackgroundPadding = 10.0f;
            nfloat CornerRadius = 13.0f;
            bool IsDismissed;

            public ActionSheetView () : base(new CGRect (0, 0, 100, 100))
            {
                BackgroundColor = UIColor.Clear;
                BackgroundView = new UIView ();
                AddSubview (BackgroundView);
                TableView = new UITableView (Bounds, UITableViewStyle.Plain);
                TableView.ClipsToBounds = true;
                TableView.Layer.CornerRadius = CornerRadius;
                TableView.RowHeight = 57.0f;
                TableView.AlwaysBounceVertical = false;
                TableView.SeparatorInset = UIEdgeInsets.Zero;
                AddSubview (TableView);
                CancelButton = new UIButton (UIButtonType.Custom);
                CancelButton.SetTitle ("Cancel", UIControlState.Normal);
                CancelButton.ClipsToBounds = true;
                CancelButton.Layer.CornerRadius = CornerRadius;
                AddSubview (CancelButton);
                AdoptTheme (Theme.Active);
                SetNeedsLayout ();
            }

            public void AdoptTheme (Theme theme)
            {
                BackgroundView.BackgroundColor = theme.ActionSheetBackgroundColor;
                TableView.AdoptTheme (theme);
                CancelButton.BackgroundColor = theme.ActionSheetItemBackgroundColor;
                CancelButton.SetTitleColor (theme.ActionSheetItemTextColor, UIControlState.Normal);
                CancelButton.TitleLabel.Font = theme.DefaultFont.WithSize (17.0f);
            }

            public override void LayoutSubviews ()
            {
                BackgroundView.Frame = Bounds;
                var cancelHeight = TableView.RowHeight;
                CancelButton.Frame = new CGRect (BackgroundPadding, Bounds.Height - cancelHeight - BackgroundPadding, Bounds.Width - BackgroundPadding - BackgroundPadding, cancelHeight);
                var tableHeight = TableView.ContentSize.Height;
                var availbleHeight = CancelButton.Frame.Y - CancelTableSpacing - BackgroundPadding;
                if (tableHeight > availbleHeight){
                    tableHeight = availbleHeight;
                }
                TableView.Frame = new CGRect (BackgroundPadding, CancelButton.Frame.Y - tableHeight - CancelTableSpacing, Bounds.Width - BackgroundPadding - BackgroundPadding, tableHeight);
                if (IsDismissed){
                    var offsetY = Bounds.Height - TableView.Frame.Y;
                    TableView.Center = new CGPoint(TableView.Center.X, TableView.Center.Y + offsetY);
                    CancelButton.Center = new CGPoint (CancelButton.Center.X, CancelButton.Center.Y + offsetY);
                }
            }

            public void ConfigureForDismissed ()
            {
                BackgroundView.Alpha = 0.0f;
                IsDismissed = true;
                SetNeedsLayout ();
            }

            public void ConfigureForPresented ()
			{
				BackgroundView.Alpha = 1.0f;
                IsDismissed = false;
				SetNeedsLayout ();
            }
        }

        class ActionCell : UITableViewCell, ThemeAdopter
        {
            public ActionCell(IntPtr handle) : base(handle)
            {
            }

            public void AdoptTheme (Theme theme){
                TextLabel.Font = theme.DefaultFont.WithSize (17.0f);
                TextLabel.TextColor = theme.ActionSheetItemTextColor;
            }
        }
    }

    public class ActionSheetItem
    {
        public string Title { get; private set; }
		public Action Action { get; private set; }
		public String AccessoryImageName { get; private set; }
        public Action AccessoryAction { get; private set; }
        public bool DismissesSheet { get; private set; }

        public ActionSheetItem (string title, Action action, string accessoryImageName = null, Action accessoryAction = null, bool dismissesSheet = true){
            Title = title;
            Action = action;
            AccessoryImageName = accessoryImageName;
            AccessoryAction = accessoryAction;
            DismissesSheet = dismissesSheet;
        }
    }
}
