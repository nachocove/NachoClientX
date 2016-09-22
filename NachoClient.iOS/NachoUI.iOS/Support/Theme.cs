//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{

    public interface ThemeAdopter
    {
        void AdoptTheme (Theme theme);
    }

    public class Theme
    {

        #region General

        public nfloat DefaultFontSize { get; protected set; }
        public UIFont DefaultFont { get; protected set; }
        public UIFont MediumDefaultFont { get; protected set; }
        public UIFont BoldDefaultFont { get; protected set; }
        public UIColor DefaultTextColor { get; protected set; }
        public UIColor DisabledTextColor { get; protected set; }
        public UIColor DestructiveTextColor { get; protected set; }
        public UIColor ButtonTextColor { get; protected set; }

        #endregion

        #region Navigation

        public bool IsNavigationBarOpaque { get; protected set; }
        public UIColor NavigationBarBackgroundColor { get; protected set; }
        public UIColor NavigationBarTintColor { get; protected set; }
        public UIImage NavigationBarShadowImage { get; protected set; }
        public UIImage NavigationBarBackgroundImage { get; protected set; }
        public UIColor NavigationBarTitleColor { get; protected set; }
        public UIFont NavigationBarTitleFont { get; protected set; }
        public UIFont NavigationBarButtonFont { get; protected set; }

        #endregion

        #region Toolbar

        public bool IsToolbarOpaque { get; protected set; }
        public UIColor ToolbarBackgroundColor { get; protected set; }
        public UIColor ToolbarTintColor { get; protected set; }

        #endregion

        #region Tabbar

        public bool IsTabBarOpaque { get; protected set; }
        public UIColor TabBarBackgroundColor { get; protected set; }
        public UIColor TabBarTintColor { get; protected set; }
        public UIColor TabBarSelectedColor { get; protected set; }
        public UIFont TabBarButtonFont { get; protected set; }

        #endregion

        #region Tables

        public UIColor TableViewGroupedBackgroundColor { get; protected set; }
        public UIColor TableViewTintColor { get; protected set; }
        public UIColor TableViewCellMainLabelTextColor { get; protected set; }
        public UIColor TableViewCellDetailLabelTextColor { get; protected set; }
        public UIColor TableViewCellDateLabelTextColor { get; protected set; }
        public UIColor TableViewCellDisclosureAccessoryColor { get; protected set; }
        public UIColor TableViewCellActionAccessoryColor { get; protected set; }
        public UIColor TableSectionHeaderTextColor { get; protected set; }

        #endregion

        #region Filterbar

        public UIColor FilterbarBackgroundColor { get; protected set; }
        public UIColor FilterbarBorderColor { get; protected set; }
        public UIColor FilterbarItemColor { get; protected set; }
        public UIColor FilterbarSelectedItemColor { get; protected set; }

        #endregion

        #region Account Creation

        public UIColor AccountCreationBackgroundColor { get; protected set; }
        public UIColor AccountCreationButtonColor { get; protected set; }
        public UIColor AccountCreationButtonTitleColor { get; protected set; }
        public UIColor AccountCreationTextColor { get; protected set; }
        public UIColor AccountCreationFieldLabelTextColor { get; protected set; }

        #endregion

        #region Account Switcher

        public UIColor AccountSwitcherTextColor { get; protected set; }

        #endregion

        #region Message

        public UIColor MessageIntentTextColor { get; protected set; }
        public UIColor ThreadIndicatorColor { get; protected set; }

        #endregion

        #region Action

        public UIColor ActionCheckboxColorHot { get; protected set; }

        #endregion

        public Theme ()
        {
            DefaultFontSize = UIFont.SystemFontSize;
            DefaultFont = UIFont.SystemFontOfSize (DefaultFontSize);
            BoldDefaultFont = UIFont.BoldSystemFontOfSize (DefaultFontSize);
            MediumDefaultFont = UIFont.SystemFontOfSize(DefaultFontSize, UIFontWeight.Medium);
        }

        static Theme _active;
        static public Theme Active {
            get {
                if (_active == null) {
                    _active = new ApolloTheme ();
                }
                return _active;
            }    
            set {
                _active = value;
            }
        }

        public virtual void DefineAppearance ()
        {
            // navigation
            UINavigationBar.Appearance.SetBackgroundImage (NavigationBarBackgroundImage, UIBarMetrics.Default);
            UINavigationBar.Appearance.BarTintColor = NavigationBarBackgroundColor;
            UINavigationBar.Appearance.TintColor = NavigationBarTintColor;
            UINavigationBar.Appearance.ShadowImage = NavigationBarShadowImage;
            UINavigationBar.Appearance.Translucent = !IsNavigationBarOpaque;
            UINavigationBar.Appearance.SetTitleTextAttributes (NavigationBarTitleTextAttributes);
            UINavigationBar.Appearance.BarStyle = IsNavigationBarOpaque ? UIBarStyle.BlackOpaque : UIBarStyle.BlackTranslucent;
            UIBarButtonItem.Appearance.SetTitleTextAttributes (NavigationBarButtonTextAttributes, UIControlState.Normal);

            // toolbar
            UIToolbar.Appearance.BarTintColor = ToolbarBackgroundColor;
            UIToolbar.Appearance.TintColor = ToolbarTintColor;

            // tabbar
            UITabBar.Appearance.BarTintColor = TabBarBackgroundColor;
            UITabBar.Appearance.TintColor = TabBarTintColor;
            UITabBar.Appearance.SelectedImageTintColor = TabBarSelectedColor;
            UITabBarItem.Appearance.SetTitleTextAttributes (TabBarButtonTextAttributes (TabBarTintColor), UIControlState.Normal);
            UITabBarItem.Appearance.SetTitleTextAttributes (TabBarButtonTextAttributes (TabBarSelectedColor), UIControlState.Selected);
        }

        private UITextAttributes NavigationBarTitleTextAttributes {
            get {
                var attributes = new UITextAttributes ();
                attributes.Font = NavigationBarTitleFont;
                attributes.TextColor = NavigationBarTitleColor;
                return attributes;
            }
        }

        private UITextAttributes NavigationBarButtonTextAttributes {
            get {
                var attributes = new UITextAttributes ();
                attributes.Font = NavigationBarButtonFont;
                return attributes;
            }
        }

        private UITextAttributes TabBarButtonTextAttributes (UIColor color) {
            var attributes = new UITextAttributes ();
            attributes.Font = NavigationBarButtonFont;
            attributes.TextColor = color;
            return attributes;
        }
    }

    class ApolloTheme : Theme
    {

        private UIColor MainColor = UIColor.FromRGBA (0x0C, 0x50, 0x66, 0xFF);
        private UIColor MainShadedColor = UIColor.FromRGBA (0x48, 0x7E, 0x92, 0xFF);
        private UIColor MainShadedColor2 = UIColor.FromRGBA (0x63, 0x93, 0x9E, 0xFF);
        private UIColor AccentColor = UIColor.FromRGBA (0xF4, 0x7D, 0x71, 0xFF);
        private UIColor ShadedColor = UIColor.FromRGBA (0xE6, 0xE7, 0xE8, 0xFF);
        private UIColor ShadedColor2 = UIColor.FromRGBA (0xFA, 0xFA, 0xFA, 0xFF);

        public ApolloTheme ()
        {

            DefaultTextColor = UIColor.FromRGBA (0x33, 0x33, 0x33, 0xFF);
            DisabledTextColor = UIColor.FromRGBA (0x77, 0x77, 0x77, 0xFF);
            DestructiveTextColor = UIColor.FromRGBA (0xEB, 0x4C, 0x2F, 0xFF);
            ButtonTextColor = MainColor;
            
            // Navigation
            IsNavigationBarOpaque = true;
            NavigationBarBackgroundColor = MainColor;
            NavigationBarTintColor = ShadedColor2;
            NavigationBarShadowImage = new UIImage ();
            NavigationBarBackgroundImage = new UIImage ();
            NavigationBarTitleColor = UIColor.White;

            // Toolbar
            IsToolbarOpaque = true;
            ToolbarBackgroundColor = ShadedColor2;
            ToolbarTintColor = MainColor;

            // TabBar
            IsTabBarOpaque = true;
            TabBarBackgroundColor = ShadedColor2;
            TabBarTintColor = TabBarBackgroundColor.ColorDarkenedByAmount (0.5f);
            TabBarSelectedColor = MainColor;

            // Tables
            TableViewGroupedBackgroundColor = ShadedColor;
            TableViewTintColor = MainColor;
            TableViewCellMainLabelTextColor = MainColor;
            TableViewCellDetailLabelTextColor = UIColor.FromRGBA (0x77, 0x77, 0x77, 0xFF);
            TableViewCellDateLabelTextColor = UIColor.FromRGBA (0x77, 0x77, 0x77, 0xFF);
            TableViewCellDisclosureAccessoryColor = MainColor;
            TableViewCellActionAccessoryColor = MainColor;
            TableSectionHeaderTextColor = TableViewGroupedBackgroundColor.ColorDarkenedByAmount (0.6f);

            // Filterbar
            FilterbarBackgroundColor = ShadedColor;
            FilterbarBorderColor = ShadedColor.ColorDarkenedByAmount (0.15f);
            FilterbarItemColor = FilterbarBorderColor;
            FilterbarSelectedItemColor = MainColor;

            // Startup
            AccountCreationBackgroundColor = MainColor;
            AccountCreationButtonColor = AccentColor;
            AccountCreationButtonTitleColor = UIColor.White;
            AccountCreationTextColor = UIColor.White;
            AccountCreationFieldLabelTextColor = MainColor;

            // Account Switcher
            AccountSwitcherTextColor = UIColor.White;

            // Message
            MessageIntentTextColor = UIColor.FromRGB (0xD2, 0x47, 0x47);
            ThreadIndicatorColor = MainShadedColor2;

            // Action
            ActionCheckboxColorHot = UIColor.FromRGB (0xEE, 0x70, 0x5B);
        }

        public override void DefineAppearance ()
        {
            base.DefineAppearance ();

            using (var arrow = UIImage.FromFile ("nav-backarrow")) {
                UINavigationBar.Appearance.BackIndicatorImage = arrow;
                UINavigationBar.Appearance.BackIndicatorTransitionMaskImage = arrow;
            }
        }

    }

    static class ThemeHelpers
    {
        public static UIFont WithFace (this UIFont font, string face)
        {
            var descriptor = font.FontDescriptor.CreateWithFace (face);
            return UIFont.FromDescriptor (descriptor, font.PointSize);
        }

        public static UIImage WithColor (this UIImage image, UIColor color)
        {
            UIGraphics.BeginImageContextWithOptions (image.Size, false, image.CurrentScale);
            var context = UIGraphics.GetCurrentContext ();
            var rect = new CGRect (0, 0, image.Size.Width, image.Size.Height);
            context.TranslateCTM (0, image.Size.Height);
            context.ScaleCTM (1, -1);
            context.ClipToMask (rect, image.CGImage);
            context.SetFillColor (color.CGColor);
            context.FillRect (rect);
            var coloredImage = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return coloredImage;
        }

        public static void AdoptTheme (this UITableView tableView, Theme theme)
        {
            var cells = tableView.VisibleCells;
            if (cells != null) {
                foreach (var cell in cells) {
                    var themed = cell as ThemeAdopter;
                    if (themed != null) {
                        themed.AdoptTheme (theme);
                    }
                }
            }
        }
    }

}
