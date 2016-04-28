using System;
using System.Collections.Generic;
using UIKit;
using Foundation;
using CoreGraphics;
using CoreAnimation;

namespace NachoClient.iOS
{

    public interface SwipeTableViewDelegate
    {
        List<SwipeTableRowAction> ActionsForSwipingRightInRow (UITableView tableView, NSIndexPath indexPath);

        List<SwipeTableRowAction> ActionsForSwipingLeftInRow (UITableView tableView, NSIndexPath indexPath);

        void WillBeginSwiping (UITableView tableView, NSIndexPath indexPath);

        void DidEndSwiping (UITableView tableView, NSIndexPath indexPath);
    }

    public class SwipeTableRowAction : SwipeAction
    {

        public Action<NSIndexPath> Handler;

        public SwipeTableRowAction (string title, UIImage image, UIColor color, Action<NSIndexPath> handler) : base (title, image, color)
        {
            Handler = handler;
        }

    }

    public class SwipeTableViewCell : UITableViewCell, SwipeActionsViewDelegate
    {

        #region Enums

        [Flags]
        public enum GroupPosition
        {
            First = 1,
            Last = 1 << 1
        }

        #endregion

        #region Properties

        public UIColor GroupBorderColor;
        public UIColor GroupSeparatorColor;
        public nfloat GroupCornerRadius = 6.0f;
        public nfloat GroupBorderWidth = 0.5f;
        public nfloat GroupSeparatorWidth = 0.5f;
        public nfloat DetailTextSpacing = 3.0f;
        public bool HideDetailWhenEmpty = false;
        private UIView _GroupSelectedBackgroundView;

        private UIView GroupSelectedBackgroundView {
            get {
                if (_GroupSelectedBackgroundView == null) {
                    _GroupSelectedBackgroundView = new UIView ();
                }
                return _GroupSelectedBackgroundView;
            }
        }

        private bool ShowingAlertateBackgroundColors;
        private List<Tuple<UIView, UIColor>> PreservedBackgroundColors;
        private UIColor _SelectedBackgroundColor = UIColor.FromRGB (0xE0, 0xE0, 0xE0);

        public UIColor SelectedBackgroundColor {
            get {
                return _SelectedBackgroundColor;
            }
            set {
                _SelectedBackgroundColor = value;
                SelectedBackgroundView.BackgroundColor = value;
            }
        }

        #endregion

        #region UITableViewCell Property Overrides

        public new UIView ContentView { get; private set; }

        private SwipeActionsView SwipeView;
        private UILabel _TextLabel;

        public new UILabel TextLabel {
            get {
                if (_TextLabel == null) {
                    _TextLabel = new UILabel (ContentView.Bounds);
                    _TextLabel.Lines = 1;
                    ContentView.AddSubview (_TextLabel);
                }
                return _TextLabel;
            }
        }

        private UILabel _DetailTextLabel;

        public new UILabel DetailTextLabel {
            get {
                if (_DetailTextLabel == null) {
                    _DetailTextLabel = new UILabel (ContentView.Bounds);
                    _DetailTextLabel.Lines = 1;
                    ContentView.InsertSubviewAbove (_DetailTextLabel, TextLabel); // ensures TextLabel gets created
                }
                return _DetailTextLabel;
            }
        }

        public new UIImageView ImageView {
            get {
                throw new NotImplementedException ("ImageView is not availalbe for a SwipeTableViewCell.  You must create your own views.");
            }
        }

        public new UITableViewCellAccessory Accessory {
            get {
                throw new NotImplementedException ("Accessory is not availalbe for a SwipeTableViewCell.  Use AccessoryView instead.");
            }
            set {
                throw new NotImplementedException ("Accessory is not availalbe for a SwipeTableViewCell.  Use AccessoryView instead.");
            }
        }

        private UIView _AccessoryView;

        public new UIView AccessoryView {
            get {
                return _AccessoryView;
            }
            set {
                if (_AccessoryView != null) {
                    _AccessoryView.RemoveFromSuperview ();
                }
                _AccessoryView = value;
                if (_AccessoryView != null) {
                    SwipeView.ContentView.AddSubview (AccessoryView);
                    SetNeedsLayout ();
                }
            }
        }

        #endregion

        #region Helper Properties

        protected UITableView TableView {
            get {
                if (Superview == null) {
                    return null;
                } else {
                    var view = Superview;
                    var tableView = view as UITableView;
                    while (tableView == null && view != null) {
                        view = view.Superview;
                        tableView = view as UITableView;
                    }
                    return tableView;
                }
            }
        }

        private SwipeTableViewDelegate TableViewDelegate {
            get {
                if (TableView != null) {
                    var tableDelegate = TableView.Delegate as SwipeTableViewDelegate;
                    if (tableDelegate == null) {
                        tableDelegate = TableView.WeakDelegate as SwipeTableViewDelegate;
                    }
                    if (tableDelegate == null) {
                        tableDelegate = TableView.Source as SwipeTableViewDelegate;
                    }
                    return tableDelegate;
                }
                return null;
            }
        }

        #endregion

        #region Constructors

        public SwipeTableViewCell (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        public SwipeTableViewCell (string reuseIndentifer) : base (UITableViewCellStyle.Default, reuseIndentifer)
        {
            Initialize ();
        }

        public SwipeTableViewCell () : base (UITableViewCellStyle.Default, null)
        {
            Initialize ();
        }

        void Initialize ()
        {
            SelectedBackgroundView = new UIView ();
            SelectedBackgroundView.BackgroundColor = SelectedBackgroundColor;
            SwipeView = new SwipeActionsView (base.ContentView.Bounds);
            SwipeView.ClipsToBounds = true;
            SwipeView.Delegate = this;
            SwipeView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            ContentView = new UIView (SwipeView.ContentView.Bounds);
            ContentView.BackgroundColor = UIColor.White;
            base.ContentView.AddSubview (SwipeView);
            SwipeView.ContentView.AddSubview (ContentView);
        }

        #endregion

        #region Cleanup

        public virtual void Cleanup ()
        {
            SwipeView.Delegate = null;
            SwipeView.Cleanup ();
        }

        #endregion

        #region Layout

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            CGRect frame;
            // AccessoryView
            if (_AccessoryView != null) {
                frame = _AccessoryView.Frame;
                frame.X = SwipeView.Bounds.Width - frame.Width;
                frame.Y = 0.0f;
                frame.Height = SwipeView.Bounds.Height - frame.Y;
                _AccessoryView.Frame = frame;
            }
            // ContentView
            frame = ContentView.Frame;
            frame.Width = (_AccessoryView != null ? _AccessoryView.Frame.X : SwipeView.Bounds.Width);
            frame.Height = SwipeView.Bounds.Height;
            ContentView.Frame = frame;
            var combinedTextLabelRect = new CGRect ();
            combinedTextLabelRect.X = SeparatorInset.Left;
            combinedTextLabelRect.Width = ContentView.Bounds.Width - combinedTextLabelRect.X;
            combinedTextLabelRect.Height = 0.0f;
            bool showDetail = _DetailTextLabel != null && (!HideDetailWhenEmpty || !String.IsNullOrWhiteSpace (_DetailTextLabel.Text));
            if (showDetail) {
                combinedTextLabelRect.Height += _DetailTextLabel.Font.RoundedLineHeight (1.0f) + DetailTextSpacing;
            }
            if (_TextLabel != null) {
                combinedTextLabelRect.Height += _TextLabel.Font.RoundedLineHeight (1.0f);
            }
            combinedTextLabelRect.Y = (ContentView.Bounds.Height - combinedTextLabelRect.Height) / 2.0f;
            // TextLabel
            if (_TextLabel != null) {
                frame = combinedTextLabelRect.Inset (0.0f, 0.0f);
                frame.Height = _TextLabel.Font.RoundedLineHeight (1.0f);
                _TextLabel.Frame = frame;
            }
            // DetailTextLabel
            if (showDetail) {
                frame = combinedTextLabelRect.Inset (0.0f, 0.0f);
                frame.Height = _DetailTextLabel.Font.RoundedLineHeight (1.0f);
                frame.Y = combinedTextLabelRect.Y + combinedTextLabelRect.Height - frame.Height;
                _DetailTextLabel.Frame = frame;
            }
            // Layers
            LayoutLayers ();
        }

        void LayoutLayers ()
        {
            // borders
            if (GroupedLeftBorder != null) {
                GroupedLeftBorder.Frame = new CGRect (SwipeView.Layer.Frame.X - GroupedLeftBorder.Frame.Width, 0.0f, GroupedLeftBorder.Frame.Width, GroupedLeftBorder.SuperLayer.Bounds.Height);
            }
            if (GroupedRightBorder != null) {
                GroupedRightBorder.Frame = new CGRect (SwipeView.Layer.Frame.X + SwipeView.Layer.Frame.Width, 0.0f, GroupedLeftBorder.Frame.Width, GroupedLeftBorder.SuperLayer.Bounds.Height);
            }
            if (GroupedSeparator != null && GroupedSeparator.SuperLayer != null) {
                GroupedSeparator.Frame = new CGRect (SwipeView.Layer.Frame.X + SeparatorInset.Left, 0.0f, SwipeView.Layer.Frame.Width - SeparatorInset.Left - SeparatorInset.Right, GroupedSeparator.Frame.Height);
            }
            if (GroupedTopBorder != null && GroupedTopBorder.SuperLayer != null) {
                GroupedTopBorder.Frame = new CGRect (GroupedLeftBorder.Frame.X, 0.0f, GroupedRightBorder.Frame.X + GroupedRightBorder.Frame.Width - GroupedLeftBorder.Frame.X, GroupedTopBorder.Frame.Height);
            }
            if (GroupedBottomBorder != null && GroupedBottomBorder.SuperLayer != null) {
                GroupedBottomBorder.Frame = new CGRect (GroupedLeftBorder.Frame.X, GroupedBottomBorder.SuperLayer.Bounds.Height - GroupedBottomBorder.Frame.Height, GroupedRightBorder.Frame.X + GroupedRightBorder.Frame.Width - GroupedLeftBorder.Frame.X, GroupedBottomBorder.Frame.Height);
            }

            // corners
            if (GroupedTopLeftCorner != null && GroupedTopLeftCorner.SuperLayer != null) {
                GroupedTopLeftCorner.Frame = new CGRect (GroupedLeftBorder.Frame.X, 0.0f, GroupedTopLeftCorner.Frame.Width, GroupedTopLeftCorner.Frame.Height);
            }
            if (GroupedTopRightCorner != null && GroupedTopRightCorner.SuperLayer != null) {
                GroupedTopRightCorner.Frame = new CGRect (GroupedRightBorder.Frame.X + GroupedRightBorder.Frame.Width - GroupedTopRightCorner.Frame.Width, 0.0f, GroupedTopRightCorner.Frame.Width, GroupedTopRightCorner.Frame.Height);
            }
            if (GroupedBottomLeftCorner != null && GroupedBottomLeftCorner.SuperLayer != null) {
                GroupedBottomLeftCorner.Frame = new CGRect (GroupedLeftBorder.Frame.X, GroupedBottomLeftCorner.SuperLayer.Bounds.Height - GroupedBottomLeftCorner.Frame.Height, GroupedBottomLeftCorner.Frame.Width, GroupedBottomLeftCorner.Frame.Height);
            }
            if (GroupedBottomRightCorner != null && GroupedBottomRightCorner.SuperLayer != null) {
                GroupedBottomRightCorner.Frame = new CGRect (GroupedRightBorder.Frame.X + GroupedRightBorder.Frame.Width - GroupedBottomRightCorner.Frame.Width, GroupedBottomRightCorner.SuperLayer.Bounds.Height - GroupedBottomRightCorner.Frame.Height, GroupedBottomRightCorner.Frame.Width, GroupedBottomRightCorner.Frame.Height);
            }
        }

        #endregion

        #region State Management

        public override void WillTransitionToState (UITableViewCellState mask)
        {
            base.WillTransitionToState (mask);
            if ((mask & UITableViewCellState.ShowingEditControlMask) != 0) {
                SwipeView.Enabled = false;
            }
        }

        public override void DidTransitionToState (UITableViewCellState mask)
        {
            base.DidTransitionToState (mask);
            if ((mask & UITableViewCellState.ShowingEditControlMask) == 0) {
                SwipeView.Enabled = true;
            }
        }

        public void EndSwiping ()
        {
            SwipeView.EndEditing ();
        }

        public override void SetEditing (bool editing, bool animated)
        {
            base.SetEditing (editing, animated);
            if (animated) {
                var keys = base.ContentView.Layer.AnimationKeys;
                CAAnimation animation;
                if (keys != null && keys.Length > 0) {
                    animation = base.ContentView.Layer.AnimationForKey (keys [0]);
                    // It's unclear why the layer animations have different duration and timing function properites
                    // than the view animations, but because they do, they're out of sync by default.  But if we
                    // replace the animations with new ones having the correct timing, everything looks good
                    SyncLayerAnimations (GroupedLeftBorder, animation);
                    SyncLayerAnimations (GroupedRightBorder, animation);
                    SyncLayerAnimations (GroupedTopBorder, animation);
                    SyncLayerAnimations (GroupedBottomBorder, animation);
                    SyncLayerAnimations (GroupedSeparator, animation);
                    SyncLayerAnimations (GroupedTopLeftCorner, animation);
                    SyncLayerAnimations (GroupedTopRightCorner, animation);
                    SyncLayerAnimations (GroupedBottomLeftCorner, animation);
                    SyncLayerAnimations (GroupedBottomRightCorner, animation);
                }

            }
        }

        static void SyncLayerAnimations (CALayer layer, CAAnimation animation)
        {
            if (layer != null) {
                var keys = layer.AnimationKeys;
                if (keys != null) {
                    foreach (var key in keys) {
                        var layerAnimation = layer.AnimationForKey (key) as CABasicAnimation;
                        if (layerAnimation != null) {
                            layer.RemoveAnimation (key);
                            var newAnimation = CABasicAnimation.FromKeyPath (layerAnimation.KeyPath);
                            newAnimation.From = layerAnimation.From;
                            newAnimation.To = layerAnimation.To;
                            newAnimation.Duration = animation.Duration;
                            newAnimation.TimingFunction = animation.TimingFunction;
                            layer.AddAnimation (newAnimation, key);
                        }
                    }
                }
            }
        }

        #endregion

        #region Grouped Style

        public void ConfigureForGroupStyle (GroupPosition position, UIColor tableBackgroundColor, nfloat inset)
        {
            base.BackgroundColor = tableBackgroundColor;
            base.ContentView.BackgroundColor = tableBackgroundColor;
            SwipeView.Frame = base.ContentView.Bounds.Inset (inset, 0.0f);
            SelectedBackgroundView = GroupSelectedBackgroundView;
            GroupSelectedBackgroundView.BackgroundColor = tableBackgroundColor;
            ConfigureBordersForGroupPosition (position, tableBackgroundColor);
        }

        CALayer GroupedLeftBorder;
        CALayer GroupedRightBorder;
        CALayer GroupedSeparator;
        CALayer GroupedTopBorder;
        CALayer GroupedBottomBorder;
        CornerLayer GroupedTopLeftCorner;
        CornerLayer GroupedTopRightCorner;
        CornerLayer GroupedBottomLeftCorner;
        CornerLayer GroupedBottomRightCorner;

        protected void ConfigureBordersForGroupPosition (GroupPosition position, UIColor tableBackgroundColor)
        {
            UIColor borderColor = GroupBorderColor;
            if (borderColor == null) {
                borderColor = tableBackgroundColor.ColorDarkenedByAmount (0.15f);
            }
            UIColor separatorColor = GroupSeparatorColor;
            if (separatorColor == null) {
                separatorColor = ContentView.BackgroundColor.ColorDarkenedByAmount (0.2f);
            }
            nfloat scale = Window != null ? Window.Screen.Scale : 1.0f;
            if (GroupedLeftBorder == null) {
                GroupedLeftBorder = new CALayer ();
                GroupedLeftBorder.Bounds = new CGRect (0.0f, 0.0f, GroupBorderWidth, GroupBorderWidth);
                GroupedLeftBorder.BackgroundColor = borderColor.CGColor;
                base.ContentView.Layer.AddSublayer (GroupedLeftBorder);
            }
            if (GroupedRightBorder == null) {
                GroupedRightBorder = new CALayer ();
                GroupedRightBorder.Bounds = new CGRect (0.0f, 0.0f, GroupBorderWidth, GroupBorderWidth);
                GroupedRightBorder.BackgroundColor = borderColor.CGColor;
                base.ContentView.Layer.AddSublayer (GroupedRightBorder);
            }
            if (position.HasFlag (GroupPosition.First)) {
                if (GroupedSeparator != null && GroupedSeparator.SuperLayer != null) {
                    GroupedSeparator.RemoveFromSuperLayer ();
                }
            } else {
                if (GroupedSeparator == null) {
                    GroupedSeparator = new CALayer ();
                    GroupedSeparator.Bounds = new CGRect (0.0f, 0.0f, GroupSeparatorWidth, GroupSeparatorWidth);
                    GroupedSeparator.BackgroundColor = separatorColor.CGColor;
                }
                if (GroupedSeparator.SuperLayer == null) {
                    base.ContentView.Layer.AddSublayer (GroupedSeparator);
                }
            }
            if (position == 0) {
                if (GroupedTopBorder != null && GroupedTopBorder.SuperLayer != null) {
                    GroupedTopBorder.RemoveFromSuperLayer ();
                }
                if (GroupedBottomBorder != null && GroupedBottomBorder.SuperLayer != null) {
                    GroupedBottomBorder.RemoveFromSuperLayer ();
                }
            } else {
                if (GroupedTopBorder == null) {
                    if (GroupedBottomBorder != null) {
                        GroupedTopBorder = GroupedBottomBorder;
                        GroupedBottomBorder = null;
                    } else {
                        GroupedTopBorder = new CALayer ();
                        GroupedTopBorder.Bounds = new CGRect (0.0f, 0.0f, GroupBorderWidth, GroupBorderWidth);
                        GroupedTopBorder.BackgroundColor = borderColor.CGColor;
                    }
                }
                if (GroupedTopBorder.SuperLayer == null) {
                    base.ContentView.Layer.AddSublayer (GroupedTopBorder);
                }
                if (position.HasFlag (GroupPosition.Last)) {
                    if (position.HasFlag (GroupPosition.First)) {
                        if (GroupedBottomBorder == null) {
                            GroupedBottomBorder = new CALayer ();
                            GroupedBottomBorder.Bounds = new CGRect (0.0f, 0.0f, GroupBorderWidth, GroupBorderWidth);
                            GroupedBottomBorder.BackgroundColor = borderColor.CGColor;
                        }
                        if (GroupedBottomBorder.SuperLayer == null) {
                            base.ContentView.Layer.AddSublayer (GroupedBottomBorder);
                        }
                    } else {
                        GroupedBottomBorder = GroupedTopBorder;
                        GroupedTopBorder = null;
                    }
                } else {
                    if (GroupedBottomBorder != null && GroupedBottomBorder.SuperLayer != null) {
                        GroupedBottomBorder.RemoveFromSuperLayer ();
                    }
                }
            }

            bool roundCorners = GroupCornerRadius > 0.0f;
            bool roundTopCorners = roundCorners && position.HasFlag (GroupPosition.First);
            bool roundBottomCorners = roundCorners && position.HasFlag (GroupPosition.Last);

            if (roundTopCorners) {
                if (GroupedTopLeftCorner == null) {
                    GroupedTopLeftCorner = new CornerLayer ();
                    GroupedTopLeftCorner.ContentsScale = scale;
                    GroupedTopLeftCorner.CornerBorderColor = borderColor.CGColor;
                    GroupedTopLeftCorner.CornerBackgroundColor = tableBackgroundColor.CGColor;
                    GroupedTopLeftCorner.CornerBorderWidth = GroupBorderWidth;
                    GroupedTopLeftCorner.Frame = new CGRect (0.0f, 0.0f, GroupCornerRadius, GroupCornerRadius);
                }
                if (GroupedTopRightCorner == null) {
                    GroupedTopRightCorner = new CornerLayer ();
                    GroupedTopRightCorner.ContentsScale = scale;
                    GroupedTopRightCorner.AffineTransform = CGAffineTransform.MakeScale (-1.0f, 1.0f);
                    GroupedTopRightCorner.CornerBorderColor = borderColor.CGColor;
                    GroupedTopRightCorner.CornerBackgroundColor = tableBackgroundColor.CGColor;
                    GroupedTopRightCorner.CornerBorderWidth = GroupBorderWidth;
                    GroupedTopRightCorner.Frame = new CGRect (0.0f, 0.0f, GroupCornerRadius, GroupCornerRadius);
                }
                if (GroupedTopLeftCorner.SuperLayer == null) {
                    base.ContentView.Layer.AddSublayer (GroupedTopLeftCorner);
                }
                if (GroupedTopRightCorner.SuperLayer == null) {
                    base.ContentView.Layer.AddSublayer (GroupedTopRightCorner);
                }
            } else {
                if (GroupedTopLeftCorner != null && GroupedTopLeftCorner.SuperLayer != null) {
                    GroupedTopLeftCorner.RemoveFromSuperLayer ();
                }
                if (GroupedTopRightCorner != null && GroupedTopRightCorner.SuperLayer != null) {
                    GroupedTopRightCorner.RemoveFromSuperLayer ();
                }
            }

            if (roundBottomCorners) {
                if (GroupedBottomLeftCorner == null) {
                    GroupedBottomLeftCorner = new CornerLayer ();
                    GroupedBottomLeftCorner.ContentsScale = scale;
                    GroupedBottomLeftCorner.AffineTransform = CGAffineTransform.MakeScale (1.0f, -1.0f);
                    GroupedBottomLeftCorner.CornerBorderColor = borderColor.CGColor;
                    GroupedBottomLeftCorner.CornerBackgroundColor = tableBackgroundColor.CGColor;
                    GroupedBottomLeftCorner.CornerBorderWidth = GroupBorderWidth;
                    GroupedBottomLeftCorner.Frame = new CGRect (0.0f, 0.0f, GroupCornerRadius, GroupCornerRadius);
                }
                if (GroupedBottomRightCorner == null) {
                    GroupedBottomRightCorner = new CornerLayer ();
                    GroupedBottomRightCorner.ContentsScale = scale;
                    GroupedBottomRightCorner.AffineTransform = CGAffineTransform.MakeScale (-1.0f, -1.0f);
                    GroupedBottomRightCorner.CornerBorderColor = borderColor.CGColor;
                    GroupedBottomRightCorner.CornerBackgroundColor = tableBackgroundColor.CGColor;
                    GroupedBottomRightCorner.CornerBorderWidth = GroupBorderWidth;
                    GroupedBottomRightCorner.Frame = new CGRect (0.0f, 0.0f, GroupCornerRadius, GroupCornerRadius);
                }
                if (GroupedBottomLeftCorner.SuperLayer == null) {
                    base.ContentView.Layer.AddSublayer (GroupedBottomLeftCorner);
                }
                if (GroupedBottomRightCorner.SuperLayer == null) {
                    base.ContentView.Layer.AddSublayer (GroupedBottomRightCorner);
                }
            } else {
                if (GroupedBottomLeftCorner != null && GroupedBottomLeftCorner.SuperLayer != null) {
                    GroupedBottomLeftCorner.RemoveFromSuperLayer ();
                }
                if (GroupedBottomRightCorner != null && GroupedBottomRightCorner.SuperLayer != null) {
                    GroupedBottomRightCorner.RemoveFromSuperLayer ();
                }
            }
        }

        #endregion

        #region Selection Behaviors

        public override void SetSelected (bool selected, bool animated)
        {
            if (selected) {
                PreserveBackgroundColors ();
            }
            base.SetSelected (selected, animated);
            if (selected) {
                RestoreBackgroundColors ();
                PreserveBackgroundColors ();
            }
            if (animated) {
                UIView.BeginAnimations (null);
                UIView.SetAnimationDuration (0.25f);
            }
            if (selected) {
                ShowSelectedBackgroundColor ();
            } else {
                RestoreBackgroundColors ();
            }
            if (animated) {
                UIView.CommitAnimations ();
            }
        }

        public override void SetHighlighted (bool highlighted, bool animated)
        {
            if (Selected) {
                base.SetHighlighted (highlighted, animated);
            } else {
                if (highlighted) {
                    PreserveBackgroundColors ();
                }
                base.SetHighlighted (highlighted, animated);
                if (highlighted) {
                    RestoreBackgroundColors ();
                    PreserveBackgroundColors ();
                }
                if (animated) {
                    UIView.BeginAnimations (null);
                    UIView.SetAnimationDuration (0.25f);
                }
                if (highlighted) {
                    ShowSelectedBackgroundColor ();
                } else {
                    RestoreBackgroundColors ();
                }
                if (animated) {
                    UIView.CommitAnimations ();
                }
            }
        }

        void ShowSelectedBackgroundColor ()
        {
            SwipeView.ContentView.BackgroundColor = SelectedBackgroundColor;
            ContentView.BackgroundColor = UIColor.Clear;
            if (_AccessoryView != null) {
                _AccessoryView.BackgroundColor = UIColor.Clear;
            }
        }

        void PreserveBackgroundColors ()
        {
            if (!ShowingAlertateBackgroundColors) {
                ShowingAlertateBackgroundColors = true;
                PreservedBackgroundColors = new List<Tuple<UIView, UIColor>> ();
                var views = new Stack<UIView> ();
                views.Push (base.ContentView);
                UIView view;
                while (views.Count > 0) {
                    view = views.Pop ();
                    PreservedBackgroundColors.Add (new Tuple<UIView, UIColor> (view, view.BackgroundColor));
                    foreach (var subview in view.Subviews) {
                        views.Push (subview);
                    }
                }
            }
        }

        void RestoreBackgroundColors ()
        {
            if (ShowingAlertateBackgroundColors) {
                foreach (var pair in PreservedBackgroundColors) {
                    pair.Item1.BackgroundColor = pair.Item2;
                }
                PreservedBackgroundColors.Clear ();
                ShowingAlertateBackgroundColors = false;
            }
        }

        #endregion

        #region Swipe View Delegate

        public List<SwipeAction> ActionsForViewSwipingRight (SwipeActionsView view)
        {
            if (TableViewDelegate != null) {
                var indexPath = TableView.IndexPathForCell (this);
                var actions = TableViewDelegate.ActionsForSwipingRightInRow (TableView, indexPath);
                if (actions != null) {
                    return new List<SwipeAction> (actions);
                }
            }
            return null;
        }

        public List<SwipeAction> ActionsForViewSwipingLeft (SwipeActionsView view)
        {
            if (TableViewDelegate != null) {
                var indexPath = TableView.IndexPathForCell (this);
                var actions = TableViewDelegate.ActionsForSwipingLeftInRow (TableView, indexPath);
                if (actions != null) {
                    return new List<SwipeAction> (actions);
                }
            }
            return null;
        }

        public void SwipeViewWillBeginShowingActions (SwipeActionsView view)
        {
            if (TableViewDelegate != null) {
                var indexPath = TableView.IndexPathForCell (this);
                TableViewDelegate.WillBeginSwiping (TableView, indexPath);
            }
        }

        public void SwipeViewDidEndShowingActions (SwipeActionsView view)
        {
            if (TableViewDelegate != null) {
                var indexPath = TableView.IndexPathForCell (this);
                if (indexPath != null) {
                    TableViewDelegate.DidEndSwiping (TableView, indexPath);
                }
            }
        }

        public void SwipeViewDidSelectAction (SwipeActionsView view, SwipeAction action)
        {
            if (TableView != null) {
                var indexPath = TableView.IndexPathForCell (this);
                var rowAction = action as SwipeTableRowAction;
                if (rowAction != null && rowAction.Handler != null) {
                    rowAction.Handler (indexPath);
                }
            }
        }

        #endregion

    }
}

