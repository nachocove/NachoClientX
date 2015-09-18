// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using Foundation;
using UIKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;
using CoreAnimation;
using CoreGraphics;

namespace NachoClient.iOS
{

    #region Delegate

    public interface AccountTypeViewControllerDelegate
    {

        void AccountTypeViewControllerDidSelectService (AccountTypeViewController vc, McAccount.AccountServiceEnum service);

    }

    #endregion

    public partial class AccountTypeViewController : UICollectionViewController
    {

        #region Properties

        public AccountTypeViewControllerDelegate AccountDelegate;

        private static NSString AccountTypeCellIdentifier = (NSString)"Account";

        protected static McAccount.AccountServiceEnum[] DefaultAccountTypes = new McAccount.AccountServiceEnum[] {
            McAccount.AccountServiceEnum.Exchange,
            McAccount.AccountServiceEnum.GoogleDefault,
            McAccount.AccountServiceEnum.GoogleExchange,
            McAccount.AccountServiceEnum.HotmailExchange,
            McAccount.AccountServiceEnum.iCloud,
            McAccount.AccountServiceEnum.IMAP_SMTP,
            McAccount.AccountServiceEnum.Office365Exchange,
            McAccount.AccountServiceEnum.OutlookExchange,
            McAccount.AccountServiceEnum.Yahoo,
        };

        private McAccount.AccountServiceEnum[] accountTypes;

        #endregion

        #region Constructors

        public AccountTypeViewController (IntPtr handle) : base (handle)
        {
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
            accountTypes = DefaultAccountTypes;
        }

        #endregion

        #region Collection View Delegate & Data Source

        public override nint NumberOfSections (UICollectionView collectionView)
        {
            return 1;
        }

        public override nint GetItemsCount (UICollectionView collectionView, nint section)
        {
            return accountTypes.Length;
        }

        public override UICollectionViewCell GetCell (UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (NcAccountTypeCollectionViewCell)collectionView.DequeueReusableCell (AccountTypeCellIdentifier, indexPath);
            var accountType = accountTypes [indexPath.Item];
            var imageName = Util.GetAccountServiceImageName (accountType);
            using (var image = UIImage.FromBundle (imageName)) {
                cell.iconView.Image = image;
            }
            cell.label.Text = NcServiceHelper.AccountServiceName (accountType);
            cell.AccessibilityLabel = cell.label.Text;
            return cell;
        }

        public override void ItemHighlighted (UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (NcAccountTypeCollectionViewCell)collectionView.CellForItem (indexPath);
            cell.iconView.Alpha = 0.5f;
        }

        public override void ItemUnhighlighted (UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (NcAccountTypeCollectionViewCell)collectionView.CellForItem (indexPath);
            cell.iconView.Alpha = 1.0f;
        }

        public override void ItemSelected (UICollectionView collectionView, NSIndexPath indexPath)
        {
            if (AccountDelegate != null) {
                var accountType = accountTypes [indexPath.Item];
                Log.Info (Log.LOG_UI, "AccountTypeViewController selected {0}", accountType);
                AccountDelegate.AccountTypeViewControllerDidSelectService (this, accountType);
            }
        }

        #endregion

        #region Public Helpers

        public AccountCredentialsViewController SuggestedCredentialsViewController (McAccount.AccountServiceEnum service)
        {
            if (service == McAccount.AccountServiceEnum.GoogleDefault) {
                Log.Info (Log.LOG_UI, "GettingStartedViewController need google credentials");
                return (GoogleCredentialsViewController)Storyboard.InstantiateViewController ("GoogleCredentialsViewController");
            } else {
                Log.Info (Log.LOG_UI, "GettingStartedViewController prompting for credentials for {0}", service);
                return (AccountCredentialsViewController)Storyboard.InstantiateViewController ("AccountCredentialsViewController");
            }
        }

        #endregion

    }

}
