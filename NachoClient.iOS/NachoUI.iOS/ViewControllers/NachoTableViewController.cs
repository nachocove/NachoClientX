//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using UIKit;
using CoreGraphics;
using Foundation;

using NachoCore.Utils;

namespace NachoClient.iOS
{
	
    public partial class NachoTableViewController : UITableViewController, SwipeTableViewDelegate, IUIScrollViewDelegate
    {
        
        NSIndexPath SwipingIndexPath;
        NcActivityIndicatorView RefreshIndicator;
        UILabel RefreshLabel;
        protected nfloat RefreshIndicatorSize = 40.0f;
        protected nfloat GroupedCellInset = 10.0f;
        bool IsShowingRefreshIndicator;
        UITableViewStyle TableStyle;
        string ClassName;

        public NachoTableViewController (UITableViewStyle tableStyle) : base ()
        {
            TableStyle = tableStyle;
            ClassName = this.GetType ().Name;
        }

        public NachoTableViewController () : this (UITableViewStyle.Plain)
        {
        }

        public override void LoadView ()
        {
            base.LoadView ();
            View = TableView = new UITableView (new CGRect (0.0f, 0.0f, 320.0f, 320.0f), TableStyle);
            if (TableStyle == UITableViewStyle.Grouped) {
                TableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
            }
            TableView.WeakDelegate = this;
            TableView.WeakDataSource = this;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
        }

        public override void ViewWillAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLAPPEAR);
            base.ViewWillAppear (animated);
        }

        public override void ViewDidAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR);
            base.ViewDidAppear (animated);
        }

        public override void ViewWillDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR);
            base.ViewWillDisappear (animated);
        }

        public override void ViewDidDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR);
            base.ViewDidDisappear (animated);
        }

        void EnableRefreshControl ()
        {
            RefreshControl = new UIRefreshControl ();
            RefreshControl.AttributedTitle = new NSAttributedString ("test");
            RefreshControl.ValueChanged += HandleRefreshControlEvent;
            var refreshOverlay = new UIView (RefreshControl.Bounds);
            refreshOverlay.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            if (TableView.Style == UITableViewStyle.Grouped) {
                refreshOverlay.BackgroundColor = TableView.BackgroundColor;
            } else {
                refreshOverlay.BackgroundColor = UIColor.White;
            }
            RefreshIndicator = new NcActivityIndicatorView (new CGRect ((refreshOverlay.Bounds.Width - RefreshIndicatorSize) / 2.0f, (refreshOverlay.Bounds.Height - RefreshIndicatorSize) / 2.0f, RefreshIndicatorSize, RefreshIndicatorSize));
            RefreshIndicator.Alpha = 0.0f;
            RefreshIndicator.Speed = 1.5f;
            refreshOverlay.AddSubview (RefreshIndicator);

            RefreshLabel = new UILabel ();
            RefreshLabel.Hidden = true;
            RefreshLabel.Font = UIFont.SystemFontOfSize (12.0f);
            RefreshLabel.TextAlignment = UITextAlignment.Center;
            RefreshLabel.TextColor = refreshOverlay.BackgroundColor.ColorDarkenedByAmount (0.6f);
            refreshOverlay.AddSubview (RefreshLabel);

            RefreshControl.AddSubview (refreshOverlay);
        }

        void EndRefreshing ()
        {
            RefreshIndicator.StopAnimating ();
            RefreshControl.EndRefreshing ();
        }

        void HandleRefreshControlEvent (object sender, EventArgs e)
        {
            Refresh ();
        }

        protected void Refresh ()
        {
        }

        void EndSwiping ()
        {
            if (SwipingIndexPath != null) {
                var cell = TableView.CellAt (SwipingIndexPath) as SwipeTableViewCell;
                cell.EndSwiping ();
                SwipingIndexPath = null;
            }
        }

        [Export ("scrollViewWillBeginDragging:")]
        public virtual void DraggingStarted (UIScrollView scrollView)
        {
            EndSwiping ();
        }

        [Foundation.Export ("scrollViewShouldScrollToTop:")]
        public virtual bool ShouldScrollToTop (UIScrollView scrollView)
        {
            EndSwiping ();
            return TableView.ScrollsToTop;
        }

        [Foundation.Export ("scrollViewDidScroll:")]
        public virtual void Scrolled (UIScrollView scrollView)
        {
            if (RefreshControl != null) {
                var distancePulled = (nfloat)Math.Max (0, -RefreshControl.Frame.Y);
                if (distancePulled > 0.0f) {
                    UpdateRefreshIndicatorForPosition (distancePulled);
                    IsShowingRefreshIndicator = true;
                } else {
                    if (IsShowingRefreshIndicator) {
                        ClearRefreshIndicator ();
                        IsShowingRefreshIndicator = false;
                    }
                }
            }
        }

        void UpdateRefreshIndicatorForPosition (nfloat distancePulled)
        {
            var percentageHeightPulled = distancePulled / RefreshControl.Frame.Height;
            RefreshIndicator.Alpha = (nfloat)Math.Min (1.0f, percentageHeightPulled);
            RefreshLabel.Alpha = RefreshIndicator.Alpha;
            var centerY = RefreshIndicator.Superview.Bounds.Height / 2.0f;
            var centerX = RefreshIndicator.Superview.Bounds.Width / 2.0f;
            RefreshIndicator.Center = new CGPoint (centerX, centerY - (1.0 - percentageHeightPulled) * RefreshControl.Frame.Height);
            RefreshLabel.Frame = new CGRect (0.0f, RefreshIndicator.Frame.Y + RefreshIndicator.Frame.Height, RefreshLabel.Superview.Bounds.Width, RefreshLabel.Font.LineHeight * 2.0f);
            RefreshLabel.Hidden = String.IsNullOrEmpty (RefreshLabel.Text);
        }

        void ClearRefreshIndicator ()
        {
            RefreshIndicator.ResetOffset ();
            RefreshIndicator.Alpha = 0.0f;
            RefreshLabel.Alpha = 0.0f;
        }

        void ReconfigureGroupedRows ()
        {
            if (TableView.Style == UITableViewStyle.Grouped) {
                foreach (var indexPath in TableView.IndexPathsForVisibleRows) {
                    WillDisplay (TableView, TableView.CellAt (indexPath), indexPath);
                }
            }
        }

        public override void WillDisplay (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            if (tableView.Style == UITableViewStyle.Grouped) {
                var swipeCell = cell as SwipeTableViewCell;
                if (swipeCell != null) {
                    SwipeTableViewCell.GroupPosition position = 0;
                    if (indexPath.Row == 0) {
                        position |= SwipeTableViewCell.GroupPosition.First;
                    }
                    if (indexPath.Row == RowsInSection (tableView, indexPath.Section) - 1) {
                        position |= SwipeTableViewCell.GroupPosition.Last;
                    }
                    swipeCell.ConfigureForGroupStyle (position, tableView.BackgroundColor, GroupedCellInset);
                }
            }
        }

        public void WillBeginSwiping (UITableView tableView, NSIndexPath indexPath)
        {
            EndSwiping ();
            SwipingIndexPath = indexPath;
        }

        public void DidEndSwiping (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.IsEqual (SwipingIndexPath)) {
                SwipingIndexPath = null;
            }
        }

        public override NSIndexPath WillSelectRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.IsEqual (SwipingIndexPath)) {
                return null;
            }
            return indexPath;
        }

        public override bool ShouldHighlightRow (UITableView tableView, NSIndexPath rowIndexPath)
        {
            if (SwipingIndexPath != null) {
                if (!rowIndexPath.IsEqual (SwipingIndexPath)) {
                    EndSwiping ();
                }
                return false;
            }
            return true;
        }

        public override UITableViewCellEditingStyle EditingStyleForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return UITableViewCellEditingStyle.None;
        }

        public virtual List<SwipeTableRowAction> ActionsForSwipingRightInRow (UITableView tableView, NSIndexPath indexPath)
        {
            return null;
        }

        public virtual List<SwipeTableRowAction> ActionsForSwipingLeftInRow (UITableView tableView, NSIndexPath indexPath)
        {
            return null;
        }

    }
}

