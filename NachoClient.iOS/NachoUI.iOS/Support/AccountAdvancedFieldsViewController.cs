//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Model;

namespace NachoClient.iOS
{

    public interface AccountAdvancedFieldsViewControllerDelegate {
        void AdvancedFieldsControllerDidChange (AccountAdvancedFieldsViewController vc);
    }

    public abstract class AccountAdvancedFieldsViewController : UIViewController
    {

        public AccountAdvancedFieldsViewControllerDelegate AccountDelegate;

        public AccountAdvancedFieldsViewController (IntPtr handle) : base (handle)
        {
        }

        public abstract String IssueWithFields (String email);
        public abstract bool CanSubmitFields ();
        public abstract void PopulateFieldsWithAccount (McAccount account);
        public abstract void PopulateAccountWithFields (McAccount account);
        public abstract void UnpopulateAccount (McAccount account);
        public abstract void SetFieldsEnabled (bool enabled);
    }
}

