// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using Foundation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using UIKit;

namespace NachoClient.iOS
{
    public partial class AboutUsViewController : NachoTableViewController, ThemeAdopter
    {
        public static string PRIVACY_POLICY_KEY = "PRIVACY_POLICY";
        public static string LICENSE_AGREEMENT_KEY = "LICENSE_AGREEMENT";

        protected string url;
        protected string title;
        protected string key;
        protected bool loadFromWeb;

        UILabel versionLabel;

        const string AboutCellIdentifier = "AboutCellIdentifier";
        const string DetailCellIdentifier = "DetailCellIdentifier";

        const int SectionAbout = 0;
        const int SectionDetails = 1;

        const int AboutRowAbout = 0;
        const int DetailsRowRelease = 0;
        const int DetailsRowPrivacy = 1;
        const int DetailsRowLicense = 2;
        const int DetailsRowOpenSource = 3;

        AboutCell aboutCell;

        public AboutUsViewController () : base (UITableViewStyle.Grouped)
        {
        }

        Theme adoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (theme != adoptedTheme) {
                adoptedTheme = theme;
                TableView.BackgroundColor = theme.TableViewGroupedBackgroundColor;
                TableView.TintColor = theme.TableViewTintColor;
                AboutFooter.Label.Font = theme.BoldDefaultFont.WithSize (14.0f);
                AboutFooter.Label.TextColor = theme.TableSectionHeaderTextColor;
                TableView.AdoptTheme (theme);
            }
        }

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.RegisterClassForCellReuse (typeof (AboutCell), AboutCellIdentifier);
            TableView.RegisterClassForCellReuse (typeof (DetailCell), DetailCellIdentifier);
            aboutCell = new AboutCell ();
            aboutCell.AdoptTheme (Theme.Active);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            NavigationItem.Title = "About Apollo Mail";
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            AdoptTheme (Theme.Active);
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return 2;
        }

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            if (section == SectionAbout) {
                return 1;
            }
            if (section == SectionDetails) {
                return 4;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("NcAssert.CaseError: AboutUsViewController.RowsInSection unknown table section {0}", section));
        }

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == SectionAbout) {
                if (indexPath.Row == AboutRowAbout) {
                    aboutCell.PrepareForWidth (TableView.Bounds.Width);
                    return aboutCell.PreferredHeight;
                }
            }
            return tableView.RowHeight;
        }

        private InsetLabelView _AboutFooter;
        private InsetLabelView AboutFooter {
            get {
                if (_AboutFooter == null) {
                    _AboutFooter = new InsetLabelView ();
                    _AboutFooter.LabelInsets = new UIEdgeInsets (10.0f, GroupedCellInset + 6.0f, 5.0f, GroupedCellInset);
                    _AboutFooter.Label.Text = "Apollo Mail version " + Util.GetVersionNumber ();
                    _AboutFooter.Label.TextAlignment = UITextAlignment.Center;
                    _AboutFooter.Frame = new CGRect (0.0f, 0.0f, 100.0f, 20.0f);
                }
                return _AboutFooter;
            }
        }

        public override nfloat GetHeightForFooter (UITableView tableView, nint section)
        {
            if (section == SectionAbout) {
                return AboutFooter.PreferredHeight;
            }
            return 0.0f;
        }

        public override UIView GetViewForFooter (UITableView tableView, nint section)
        {
            if (section == SectionAbout) {
                return AboutFooter;
            }
            return null;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == SectionAbout) {
                if (indexPath.Row == AboutRowAbout) {
                    return aboutCell;
                }
            } else if (indexPath.Section == SectionDetails) {
                var cell = tableView.DequeueReusableCell (DetailCellIdentifier, indexPath) as DetailCell;
                if ((cell.AccessoryView as DisclosureAccessoryView) == null) {
                    cell.AccessoryView = new DisclosureAccessoryView ();
                }
                if (indexPath.Row == DetailsRowRelease) {
                    cell.TextLabel.Text = "Release Notes";
                    return cell;
                } else if (indexPath.Row == DetailsRowPrivacy) {
                    cell.TextLabel.Text = "Privacy Policy";
                    return cell;
                } else if (indexPath.Row == DetailsRowLicense) {
                    cell.TextLabel.Text = "License Agreement";
                    return cell;
                } else if (indexPath.Row == DetailsRowOpenSource) {
                    cell.TextLabel.Text = "Open Source Contributions";
                    return cell;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("NcAssert.CaseError: AboutUsViewController.GetCell unknown table row {0}.{1}", indexPath.Section, indexPath.Row));
        }

        public override bool ShouldHighlightRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == SectionDetails) {
                return base.ShouldHighlightRow (tableView, indexPath);
            }
            return false;
        }

        public override NSIndexPath WillSelectRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == SectionDetails) {
                return base.WillSelectRow (tableView, indexPath);
            }
            return null;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == SectionDetails) {
                if (indexPath.Row == DetailsRowRelease) {
                    ShowReleaseNotes ();
                } else if (indexPath.Row == DetailsRowPrivacy) {
                    ShowPrivacyPolicy ();
                } else if (indexPath.Row == DetailsRowLicense) {
                    ShowLicenseAgreement ();
                } else if (indexPath.Row == DetailsRowOpenSource) {
                    ShowOpenSource ();
                }
            }
        }

        private void ShowReleaseNotes ()
        {
            var vc = new SettingsLegalViewController ();
            vc.DocumentLocation = NSBundle.MainBundle.GetUrlForResource ("ReleaseNotes", "html");
            vc.NavigationItem.Title = "Release Notes";
            NavigationController.PushViewController (vc, true);
        }

        private void ShowPrivacyPolicy ()
        {
            var vc = new SettingsLegalViewController ();
            vc.DocumentLocation = new NSUrl ("https://nachocove.com/privacy-policy-text/");
            vc.NavigationItem.Title = "Privacy Policy";
            NavigationController.PushViewController (vc, true);
        }

        private void ShowLicenseAgreement ()
        {
            var vc = new SettingsLegalViewController ();
            vc.DocumentLocation = new NSUrl ("https://nachocove.com/legal-text/");
            vc.NavigationItem.Title = "License Agreement";
            NavigationController.PushViewController (vc, true);
        }

        private void ShowOpenSource ()
        {
            var vc = new SettingsLegalViewController ();
            vc.DocumentLocation = NSBundle.MainBundle.GetUrlForResource ("LegalInfo", "html");
            vc.NavigationItem.Title = "Open Source Contributions";
            NavigationController.PushViewController (vc, true);
        }

        private class AboutCell : SwipeTableViewCell, ThemeAdopter
        {

            UIImageView LogoView;
            UILabel HeaderLabel;
            UILabel DescriptionLabel;

            public AboutCell () : base ()
            {
                LogoView = new UIImageView (new CGRect (0, 0, 80, 80));
                using (var nachoLogo = UIImage.FromBundle ("AboutLogo")) {
                    LogoView.Image = nachoLogo;
                }
                ContentView.AddSubview (LogoView);

                HeaderLabel = new UILabel ();
                HeaderLabel.TextAlignment = UITextAlignment.Center;
                HeaderLabel.Lines = 0;
                HeaderLabel.LineBreakMode = UILineBreakMode.WordWrap;
                HeaderLabel.Text = "Apollo Mail believes that productivity software is more than just a great email app with contacts and calendar capability.";
                ContentView.AddSubview (HeaderLabel);

                DescriptionLabel = new UILabel ();
                DescriptionLabel.TextAlignment = UITextAlignment.Center;
                DescriptionLabel.Lines = 5;
                DescriptionLabel.LineBreakMode = UILineBreakMode.WordWrap;
                DescriptionLabel.Text = "In addition to being a great email " +
                "client, your PIM software should actively help you achieve your" +
                " goals, help you manage your time and reduce clutter that gets " +
                "in your way.";
                ContentView.AddSubview (DescriptionLabel);

                SetNeedsLayout ();
            }

            public void AdoptTheme (Theme theme)
            {
                HeaderLabel.Font = theme.BoldDefaultFont.WithSize (17.0f);
                HeaderLabel.TextColor = theme.TableViewCellMainLabelTextColor;
                DescriptionLabel.Font = theme.DefaultFont.WithSize (14.0f);
                DescriptionLabel.TextColor = theme.DefaultTextColor;
            }

            public void PrepareForWidth (nfloat width)
            {
                var frame = Frame;
                if (frame.Width != width) {
                    frame.Width = width;
                    Frame = frame;
                    SetNeedsLayout ();
                    LayoutIfNeeded ();
                }
            }

            public nfloat PreferredHeight {
                get {
                    return DescriptionLabel.Frame.Top + DescriptionLabel.Frame.Height + 15.0f;
                }
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                LogoView.Center = new CGPoint (Bounds.Width / 2.0f, LogoView.Frame.Height / 2.0f + 10.0f);

                var y = LogoView.Frame.Top + LogoView.Frame.Height + 15.0f;
                var inset = 30.0f;
                var width = ContentView.Bounds.Width - 2.0f * inset;
                var size = HeaderLabel.SizeThatFits (new CGSize (width, 0.0f));
                HeaderLabel.Frame = new CGRect (inset, y, width, size.Height);

                y += size.Height + 15.0f;
                size = DescriptionLabel.SizeThatFits (new CGSize (width, 0.0f));
                DescriptionLabel.Frame = new CGRect (inset, y, width, size.Height);
            }
        }

        private class DetailCell : SwipeTableViewCell, ThemeAdopter
        {
            
            public DetailCell (IntPtr ptr) : base (ptr)
            {
            }

            public void AdoptTheme (Theme theme)
            {
                TextLabel.Font = theme.DefaultFont.WithSize (14.0f);
                TextLabel.TextColor = theme.TableViewCellMainLabelTextColor;
            }
        }

        private class DisclosureAccessoryView : ImageAccessoryView
        {
            public DisclosureAccessoryView () : base ("gen-more-arrow")
            {
            }
        }

    }
}
