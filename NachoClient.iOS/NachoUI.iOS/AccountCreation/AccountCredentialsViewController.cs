//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Model;
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

        protected override void CreateViewHierarchy ()
        {
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
        }

        public virtual void PresentInNavigationController (UINavigationController navController)
        {
            navController.PushViewController (this, true);
        }
    }
}

