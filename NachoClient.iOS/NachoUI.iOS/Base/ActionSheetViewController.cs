//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using Foundation;
using UIKit;
using CoreGraphics;
using CoreAnimation;

namespace NachoClient.iOS
{
    public class ActionSheetViewController : NcUIViewController, IUITableViewDelegate, IUITableViewDataSource, ThemeAdopter, IUIViewControllerAnimatedTransitioning
    {

        private ActionSheetView ActionView;
        private SizedTableView TableView;
        private List<ActionSheetItem> Items = new List<ActionSheetItem> ();
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
            UIView.AnimateNotify (TransitionDuration (transitionContext), 0.0f, UIViewAnimationOptions.CurveEaseInOut, () => {
                if (IsBeingPresented) {
                    ActionView.ConfigureForPresented ();
                } else {
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
                if (presented.View.Superview != null) {
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

        public void AddItem (ActionSheetItem item)
        {
            Items.Add (item);
        }

        public void BeginReplacingItems ()
        {
            Items.Clear ();
            CreateTableView ();
        }

        public void EndReplacingItems (bool animated = true)
        {
            TableView.ReloadData ();
            TableView.LayoutIfNeeded ();
            ActionView.SetContentView (TableView, animated: animated);
        }

        public void SetContentView (UIView contentView, bool animated = false)
        {
            Items.Clear ();
            CleanupTableView ();
            TableView = null;
            (contentView as ThemeAdopter)?.AdoptTheme (AdoptedTheme ?? Theme.Active);
            ActionView.SetContentView (contentView, animated: animated);
        }

        #endregion

        #region View Lifecycle

        public override void LoadView ()
        {
            View = ActionView = new ActionSheetView ();
            CreateTableView ();
            ActionView.SetContentView (TableView, animated: false);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
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
            if (ShouldCleanupDuringDidDisappear) {
                Cleanup ();
            }
        }

        private void Cleanup ()
        {
            CleanupTableView ();
            ActionView.CancelButton.TouchUpInside -= Cancel;
            ActionView.BackgroundView.RemoveGestureRecognizer (ActionView.BackgroundView.GestureRecognizers [0]);
            TransitioningDelegate = null;
        }

        #endregion

        #region Theme

        Theme AdoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (theme != AdoptedTheme) {
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

        private void CreateTableView ()
        {
            CleanupTableView ();
            TableView = new SizedTableView (ActionView.Bounds, UITableViewStyle.Plain);
            TableView.ClipsToBounds = true;
            TableView.Layer.CornerRadius = ActionView.CornerRadius;
            TableView.RowHeight = ActionView.ButtonHeight;
            TableView.AlwaysBounceVertical = false;
            TableView.SeparatorInset = UIEdgeInsets.Zero;
            TableView.Delegate = this;
            TableView.DataSource = this;
            TableView.RegisterClassForCellReuse (typeof (ActionCell), ActionCellReuseIdentifier);
            TableView.AdoptTheme (AdoptedTheme ?? Theme.Active);
        }

        private void CleanupTableView ()
        {
            if (TableView != null) {
                TableView.Delegate = null;
                TableView.DataSource = null;
            }
        }

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
            if (item.AccessoryImageName != null) {
                var accessoryView = new ImageAccessoryView (item.AccessoryImageName);
                if (item.AccessoryAction != null) {
                    accessoryView.ImageView.UserInteractionEnabled = true;
                    accessoryView.ImageView.AddGestureRecognizer (new UITapGestureRecognizer ((recognizer) => {
                        item.AccessoryAction ();
                        if (item.DismissesSheet) {
                            Dismiss ();
                        }
                    }));
                }
                cell.AccessoryView = accessoryView;
            } else {
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
            if (item.DismissesSheet) {
                Dismiss ();
            }
        }

        #endregion

        private class ActionSheetView : UIView, ThemeAdopter
        {

            public UIView BackgroundView { get; private set; }
            public UIView ContentView { get; private set; }
            public UIView ReplacedContentView { get; private set; }
            public NcSimpleColorButton CancelButton { get; private set; }
            nfloat CancelContentSpacing = 8.0f;
            nfloat BackgroundPadding = 10.0f;
            public nfloat CornerRadius { get; private set; } = 13.0f;
            public nfloat ButtonHeight { get; private set; } = 57.0f;
            bool IsDismissed;

            public ActionSheetView () : base (new CGRect (0, 0, 100, 100))
            {
                BackgroundColor = UIColor.Clear;
                BackgroundView = new UIView ();
                AddSubview (BackgroundView);
                CancelButton = new NcSimpleColorButton ();
                CancelButton.SetTitle ("Cancel", UIControlState.Normal);
                CancelButton.ClipsToBounds = true;
                CancelButton.Layer.CornerRadius = CornerRadius;
                AddSubview (CancelButton);
                AdoptTheme (Theme.Active);
                SetNeedsLayout ();
            }

            public void SetContentView (UIView contentView, bool animated)
            {
                if (animated && ContentView != null) {
                    ReplacedContentView = ContentView;
                    ContentView = contentView;
                    InsertSubviewAbove (ContentView, ReplacedContentView);
                    var contentHeight = ContentHeight;
                    ContentView.Frame = new CGRect (BackgroundPadding + Bounds.Width, CancelButton.Frame.Y - contentHeight - CancelContentSpacing, Bounds.Width - BackgroundPadding - BackgroundPadding, contentHeight);
                    UIView.Animate (0.3f, 0.0f, UIViewAnimationOptions.CurveEaseInOut, () => {
                        ContentView.Center = new CGPoint (ContentView.Center.X - Bounds.Width, ContentView.Center.Y);
                        ReplacedContentView.Center = new CGPoint (ReplacedContentView.Center.X - Bounds.Width, ReplacedContentView.Center.Y);
                    }, () => {
                        ReplacedContentView.RemoveFromSuperview ();
                    });
                } else {
                    if (ContentView != null) {
                        ContentView.RemoveFromSuperview ();
                    }
                    ContentView = contentView;
                    InsertSubviewBelow (ContentView, CancelButton);
                    SetNeedsLayout ();
                }
            }

            public void AdoptTheme (Theme theme)
            {
                BackgroundView.BackgroundColor = theme.ActionSheetBackgroundColor;
                if (ContentView is ThemeAdopter) {
                    (ContentView as ThemeAdopter).AdoptTheme (theme);
                }
                CancelButton.BackgroundColor = theme.ActionSheetItemBackgroundColor;
                CancelButton.HighlightedColor = theme.ActionSheetItemBackgroundColor.ColorDarkenedByAmount (0.15f);
                CancelButton.SetTitleColor (theme.ActionSheetItemTextColor, UIControlState.Normal);
                CancelButton.TitleLabel.Font = theme.MediumDefaultFont.WithSize (17.0f);
            }

            public override void LayoutSubviews ()
            {
                BackgroundView.Frame = Bounds;
                var cancelHeight = ButtonHeight;
                CancelButton.Frame = new CGRect (BackgroundPadding, Bounds.Height - cancelHeight - BackgroundPadding, Bounds.Width - BackgroundPadding - BackgroundPadding, cancelHeight);
                var contentHeight = ContentHeight;
                ContentView.Frame = new CGRect (BackgroundPadding, CancelButton.Frame.Y - contentHeight - CancelContentSpacing, Bounds.Width - BackgroundPadding - BackgroundPadding, contentHeight);
                if (IsDismissed) {
                    var offsetY = Bounds.Height - ContentView.Frame.Y;
                    ContentView.Center = new CGPoint (ContentView.Center.X, ContentView.Center.Y + offsetY);
                    CancelButton.Center = new CGPoint (CancelButton.Center.X, CancelButton.Center.Y + offsetY);
                }
            }

            private nfloat ContentHeight {
                get {
                    var contentHeight = ContentView.IntrinsicContentSize.Height;
                    var availbleHeight = CancelButton.Frame.Y - CancelContentSpacing - BackgroundPadding;
                    if (contentHeight > availbleHeight) {
                        contentHeight = availbleHeight;
                    }
                    return contentHeight;
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
            public ActionCell (IntPtr handle) : base (handle)
            {
            }

            public void AdoptTheme (Theme theme)
            {
                TextLabel.Font = theme.DefaultFont.WithSize (17.0f);
                TextLabel.TextColor = theme.ActionSheetItemTextColor;
            }
        }
    }

    public class SizedTableView : UITableView
    {
        public override CGSize ContentSize {
            get {
                return base.ContentSize;
            }
            set {
                base.ContentSize = value;
                InvalidateIntrinsicContentSize ();
            }
        }

        public override CGSize IntrinsicContentSize {
            get {
                return ContentSize;
            }
        }

        public SizedTableView (CGRect frame, UITableViewStyle style) : base (frame, style)
        {
        }
    }

    public class ActionSheetItem
    {
        public string Title { get; private set; }
        public Action Action { get; private set; }
        public String AccessoryImageName { get; private set; }
        public Action AccessoryAction { get; private set; }
        public bool DismissesSheet { get; private set; }

        public ActionSheetItem (string title, Action action, string accessoryImageName = null, Action accessoryAction = null, bool dismissesSheet = true)
        {
            Title = title;
            Action = action;
            AccessoryImageName = accessoryImageName;
            AccessoryAction = accessoryAction;
            DismissesSheet = dismissesSheet;
        }
    }

    public class ActionSheetDatePicker : UIView, ThemeAdopter
    {
        UIDatePicker DatePicker;
        UIView DividerView;
        NcSimpleColorButton SelectButton;
        public nfloat CornerRadius { get; private set; } = 13.0f;
        public nfloat ButtonHeight { get; private set; } = 57.0f;

        public ActionSheetDatePicker (Action<DateTime> dateSelected) : base (new CGRect (0, 0, 100, 0))
        {
            ClipsToBounds = true;
            Layer.CornerRadius = CornerRadius;

            DatePicker = new UIDatePicker ();
            DatePicker.Frame = new CGRect (0, 0, Bounds.Width, DatePicker.IntrinsicContentSize.Height);
            DatePicker.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            DatePicker.Mode = UIDatePickerMode.Date;
            DatePicker.MinimumDate = NSCalendar.CurrentCalendar.DateByAddingUnit (NSCalendarUnit.Day, 1, NSDate.Now, NSCalendarOptions.None);
            DatePicker.Date = DatePicker.MinimumDate;
            AddSubview (DatePicker);

            DividerView = new UIView (new CGRect (0, DatePicker.Frame.Y + DatePicker.Frame.Height, Bounds.Width, 1.0f));
            DividerView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            AddSubview (DividerView);

            SelectButton = new NcSimpleColorButton ();
            SelectButton.Frame = new CGRect (0, DividerView.Frame.Y + DividerView.Frame.Height, Bounds.Width, ButtonHeight);
            SelectButton.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            SelectButton.SetTitle (NSBundle.MainBundle.LocalizedString ("Use Date", "Setter button title for the date picker"), UIControlState.Normal);
            SelectButton.TouchUpInside += (sender, e) => {
                var date = DateTime.SpecifyKind (DatePicker.Date.ToDateTime (), DateTimeKind.Utc);
                dateSelected (date);
            };
            AddSubview (SelectButton);
        }

        public void AdoptTheme (Theme theme)
        {
            TintColor = theme.TableViewTintColor;
            BackgroundColor = theme.ActionSheetItemBackgroundColor;
            DividerView.BackgroundColor = theme.TableViewGroupedBackgroundColor;
            SelectButton.TitleLabel.Font = theme.DefaultFont.WithSize (17.0f);
            SelectButton.SetTitleColor (theme.ActionSheetItemTextColor, UIControlState.Normal);
            SelectButton.BackgroundColor = theme.ActionSheetItemBackgroundColor;
            SelectButton.HighlightedColor = theme.ActionSheetItemBackgroundColor.ColorDarkenedByAmount (0.15f);
        }

        public override CGSize IntrinsicContentSize {
            get {
                var height = DatePicker.Frame.Height + DividerView.Frame.Height + SelectButton.Frame.Height;
                return new CGSize (Bounds.Width, height);
            }
        }
    }

}