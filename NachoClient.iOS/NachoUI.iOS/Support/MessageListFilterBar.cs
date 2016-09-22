//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using CoreGraphics;
using CoreAnimation;
using Foundation;

namespace NachoClient.iOS
{
    public class MessageListFilterBar : UIView, ThemeAdopter
    {

        public static readonly nfloat PreferredHeight = 32.0f;
        CornerLayer CornerLayerTemplate;
        CALayer BorderLayer;
        CAReplicatorLayer CornersLayer;
        nfloat BorderWidth = 0.5f;
        nfloat CornerRadius = 6.0f;
        public MessageFilterBarItem[] Items { get; private set; }
        List<FilterBarItemView> ItemViews;
        UIColor _UnselectedItemColor;
        UIColor _SelectedItemColor;
        UIColor _BorderColor;
        UIColor _BarColor;
        UIFont _ItemFont;
        public UIColor UnselectedItemColor {
            get {
                return _UnselectedItemColor;
            }
            set {
                _UnselectedItemColor = value;
                UpdateItemViews ();
            }
        }
        public UIColor SelectedItemColor {
            get {
                return _SelectedItemColor;
            }
            set {
                _SelectedItemColor = value;
                UpdateItemViews ();
            }
        }

        public UIColor BorderColor {
            get {
                return _BorderColor;
            }
            set {
                _BorderColor = value;
                BorderLayer.BackgroundColor = _BorderColor.CGColor;
                CornerLayerTemplate.CornerBorderColor = _BorderColor.CGColor;
            }
        }

        public UIFont ItemFont {
            get {
                return _ItemFont;
            }
            set {
                _ItemFont = value;
                UpdateItemViews ();
            }
        }

        public UIColor BarColor {
            get {
                return BackgroundColor;
            }
            set {
                BackgroundColor = value;
                CornerLayerTemplate.CornerBackgroundColor = BackgroundColor.CGColor;
            }
        }

        FilterBarItemView SelectedItemView;

        public MessageListFilterBar (CGRect frame) : base (frame)
        {

            BorderLayer = new CALayer ();

            CornerLayerTemplate = new CornerLayer ();
            CornerLayerTemplate.ContentsScale = UIScreen.MainScreen.Scale;
            CornerLayerTemplate.Frame = new CGRect (0.0f, 0.0f, CornerRadius, CornerRadius);
            CornerLayerTemplate.CornerBorderWidth = 0.5f;
            CornerLayerTemplate.BackgroundColor = UIColor.Clear.CGColor;

            CornersLayer = new CAReplicatorLayer ();
            CornersLayer.Frame = new CGRect (-BorderWidth, Layer.Bounds.Height - BorderWidth, CornerRadius, CornerRadius);
            CornersLayer.InstanceCount = 2;
            CornersLayer.AddSublayer (CornerLayerTemplate);

            Layer.AddSublayer (BorderLayer);
            Layer.AddSublayer (CornersLayer);

            ItemViews = new List<FilterBarItemView> ();
        }

        public void Cleanup ()
        {
            Items = new MessageFilterBarItem[]{ };
            foreach (var itemView in ItemViews) {
                itemView.FilterBar = null;
                itemView.Cleanup ();
            }
        }

        public void AdoptTheme (Theme theme)
        {
            BarColor = theme.FilterbarBackgroundColor;
            BorderColor = theme.FilterbarBorderColor;
            ItemFont = theme.DefaultFont;
            UnselectedItemColor = theme.FilterbarItemColor;
            SelectedItemColor = theme.FilterbarSelectedItemColor;
        }

        void UpdateItemViews ()
        {
            foreach (var view in ItemViews) {
                if (view == SelectedItemView) {
                    view.TintColor = _SelectedItemColor;
                } else {
                    view.TintColor = _UnselectedItemColor;
                }
                view.TitleLabel.Font = _ItemFont.WithSize (12.0f);
            }
        }

        public void SetItems (MessageFilterBarItem[] items)
        {
            Items = items;
            var viewQueue = new Queue<FilterBarItemView> (ItemViews);
            ItemViews = new List<FilterBarItemView> (items.Length);
            FilterBarItemView itemView;
            foreach (var item in items) {
                if (viewQueue.Count > 0) {
                    itemView = viewQueue.Dequeue ();
                } else {
                    itemView = new FilterBarItemView (new CGRect (0.0f, 0.0f, Bounds.Height, Bounds.Height));
                    itemView.FilterBar = this;
                    AddSubview (itemView);
                }
                ItemViews.Add (itemView);
                itemView.Item = item;
                itemView.TintColor = UnselectedItemColor;
            }
            foreach (var view in viewQueue) {
                view.FilterBar = null;
                view.RemoveFromSuperview ();
            }
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();

            var itemCount = ItemViews.Count;
            var scale = Window != null ? Window.Screen.Scale : 1.0f;
            var itemSize = new CGSize ((nfloat)Math.Floor (Bounds.Width / (itemCount + 1) * scale) / scale, Bounds.Height);

            var x = (Bounds.Width - itemCount * itemSize.Width) / 2.0f;
            foreach (var itemView in ItemViews) {
                itemView.Frame = new CGRect (x, 0.0f, itemSize.Width, itemSize.Height);
                x += itemSize.Width;
            }

            CATransaction.Begin ();
            var keys = Layer.AnimationKeys;
            if (keys != null && keys.Length > 0) {
                var animation = Layer.AnimationForKey (keys [0]);
                CATransaction.AnimationDuration = animation.Duration;
                CATransaction.AnimationTimingFunction = animation.TimingFunction;
            } else {
                CATransaction.DisableActions = true;
            }
            var transform = CATransform3D.MakeTranslation (Bounds.Width - CornerLayerTemplate.Frame.Width + 2.0f * BorderWidth, 0.0f, 0.0f);
            CornersLayer.InstanceTransform = transform.Scale (-1.0f, 1.0f, 1.0f);;
            BorderLayer.Frame = new CGRect (0.0f, Layer.Bounds.Height - BorderWidth, Layer.Bounds.Width, BorderWidth);
            CATransaction.Commit ();
        }

        public void SelectItem (MessageFilterBarItem item)
        {
            if (SelectedItemView != null) {
                SelectedItemView.TintColor = UnselectedItemColor;
            }
            SelectedItemView = null;
            if (item != null) {
                foreach (var itemView in ItemViews) {
                    if (itemView.Item == item) {
                        SelectedItemView = itemView;
                        SelectedItemView.TintColor = SelectedItemColor;
                        break;
                    }
                }
            }
        }

        private class FilterBarItemView : UIView
        {

            MessageFilterBarItem _Item;
            public MessageFilterBarItem Item {
                get {
                    return _Item;
                }
                set {
                    _Item = value;
                    Update ();
                }
            }
            public MessageListFilterBar FilterBar;
            UIImageView ImageView;
            public UILabel TitleLabel;
            nfloat ImageSize;
            UITapGestureRecognizer TapGestureRecognizer;

            public FilterBarItemView (CGRect frame) : base (frame)
            {
                UserInteractionEnabled = true;

                ImageSize = frame.Size.Height / 2.0f;
                ImageView = new UIImageView (new CGRect (0.0f, 0.0f, ImageSize, ImageSize));
                TitleLabel = new UILabel (new CGRect(0.0f, 0.0f, Bounds.Width, 16.0f));
                TitleLabel.TextAlignment = UITextAlignment.Center;

                AddSubview (ImageView);
                AddSubview (TitleLabel);

                TapGestureRecognizer = new UITapGestureRecognizer (Tap);
                AddGestureRecognizer (TapGestureRecognizer);
            }

            public void Cleanup ()
            {
                RemoveGestureRecognizer (TapGestureRecognizer);
                TapGestureRecognizer = null;
                _Item = null;
            }

            void Tap ()
            {
                FilterBar.SelectItem (Item);
                ShowTitle ();
                Item.Action ();
            }

            void Update ()
            {
                ImageView.Image = _Item.Image.ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate);
                TitleLabel.Text = _Item.Title;
            }

            public override void TintColorDidChange ()
            {
                base.TintColorDidChange ();
                ImageView.TintColor = TintColor;
                TitleLabel.TextColor = TintColor;
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                TitleLabel.Alpha = 0.0f;
                TitleLabel.Frame = new CGRect (0.0f, -TitleLabel.Frame.Height, Bounds.Width, TitleLabel.Frame.Height);
                ImageView.Center = new CGPoint (Bounds.Width / 2.0f, Bounds.Height / 2.0f);
            }

            private void ShowTitle ()
            {
                nfloat inDuration = 0.15f;
                nfloat onDuration = 0.8f;
                nfloat outDuration = 0.2f;
                nfloat opacity = 0.0f;
                nfloat titleY0 = -TitleLabel.Frame.Height / 2.0f;
                nfloat titleY = titleY0;
                nfloat titleY1 = 5.0f + TitleLabel.Frame.Height / 2.0f;
                nfloat imageY0 = Bounds.Height / 2.0f;
                nfloat imageY = imageY0;
                nfloat imageY1 = titleY1 + ImageView.Frame.Height;
                var animation = TitleLabel.Layer.AnimationForKey ("opacity") as CAKeyFrameAnimation;
                if (animation != null) {
                    opacity = TitleLabel.Layer.PresentationLayer.Opacity;
                    titleY = TitleLabel.Layer.Position.Y;
                    imageY = ImageView.Layer.Position.Y;
                    inDuration = inDuration * (1.0f - opacity);
                }
                TitleLabel.Layer.RemoveAllAnimations ();
                ImageView.Layer.RemoveAllAnimations ();
                var duration = inDuration + onDuration + outDuration;
                var keyTimes = new NSNumber[] {
                    new NSNumber (0.0f),
                    new NSNumber (inDuration / duration),
                    new NSNumber ((inDuration + onDuration) / duration), 
                    new NSNumber ((inDuration + onDuration + outDuration) / duration)
                };

                animation = CAKeyFrameAnimation.FromKeyPath ("opacity");
                animation.Duration = duration;
                animation.Values = new NSObject[] {
                    new NSNumber (opacity),
                    new NSNumber (1.0f),
                    new NSNumber (1.0f), 
                    new NSNumber (0.0f)
                };
                animation.KeyTimes = keyTimes;
                TitleLabel.Layer.AddAnimation (animation, "opacity");

                animation = CAKeyFrameAnimation.FromKeyPath ("position.y");
                animation.Duration = duration;
                animation.Values = new NSObject[] {
                    new NSNumber (titleY),
                    new NSNumber (titleY1),
                    new NSNumber (titleY1),
                    new NSNumber (titleY0)
                };
                animation.KeyTimes = keyTimes;
                TitleLabel.Layer.AddAnimation (animation, "position.y");

                animation = CAKeyFrameAnimation.FromKeyPath ("position.y");
                animation.Duration = duration;
                animation.Values = new NSObject[] {
                    new NSNumber (imageY),
                    new NSNumber (imageY1),
                    new NSNumber (imageY1),
                    new NSNumber (imageY0)
                };
                animation.KeyTimes = keyTimes;
                ImageView.Layer.AddAnimation (animation, "position.y");
            }
        }

    }

    public class MessageFilterBarItem
    {

        public readonly UIImage Image;
        public readonly string Title;
        public readonly Action Action;

        public MessageFilterBarItem (string title, UIImage image, Action action)
        {
            Title = title;
            Image = image;
            Action = action;
        }

    }
}

