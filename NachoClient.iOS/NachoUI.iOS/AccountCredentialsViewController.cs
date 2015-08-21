//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Model;
using Foundation;
using System.Net.Http;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public interface AccountCredentialsViewControllerDelegate
    {
        void AccountCredentialsViewControllerDidValidateAccount (AccountCredentialsViewController vc, McAccount account);
    }

    public class AccountCredentialsViewController : NcUIViewControllerNoLeaks
    {
        
        public AccountCredentialsViewControllerDelegate AccountDelegate;
        public McAccount.AccountServiceEnum Service;
        public McAccount Account;


        public AccountCredentialsViewController (IntPtr handle) : base (handle)
        {
        }

        protected async void PopulateProfilePhotoFromURL (NSUrl imageUrl)
        {
            try {
                var httpClient = new HttpClient ();
                byte[] contents = await httpClient.GetByteArrayAsync (imageUrl);
                var portrait = McPortrait.InsertFile (Account.Id, contents);
                Account.DisplayPortraitId = portrait.Id;
                Account.Update ();
            } catch (Exception e) {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController: PopulateProfilePhotoFromURL {0}", e);
            }
        }

        protected override void CreateViewHierarchy ()
        {
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
        }
    }
}

