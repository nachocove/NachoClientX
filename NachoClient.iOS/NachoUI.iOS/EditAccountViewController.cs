//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.

using System;
using System.Net;
using MonoTouch.Dialog;
using MonoTouch.UIKit;
using NachoCore.Model;
using NachoCore;

namespace NachoClient.iOS
{
    public class EditAccountViewController : NcDialogViewController
    {
        class AccountInfo
        {
            [Section ("Exchange Account", "")]

            [Password("Exchange password")]
            [Entry (AutocorrectionType = UITextAutocorrectionType.No, AutocapitalizationType = UITextAutocapitalizationType.None)]
            public string Password;
        }

        void CheckAccount (Action<string> result)
        {
            Util.PushNetworkActive ();
            // TODO: Check credentials
            bool Success = true; // Crowbar
            BeginInvokeOnMainThread (delegate {
                result (Success ? null : Locale.GetText ("Attempted to login failed"));
            });
        }

        UIAlertView dlg;

        public EditAccountViewController (McAccount account) : base (null, true)
        {
            bool newAccount = account == null;

            if (newAccount) {
                account = new McAccount ();
            } else {
                // TODO: Init info with McAccount data
            }

            var info = new AccountInfo ();

            var bc = new BindingContext (this, info, "Edit Account");
            Root = bc.Root;

            UIBarButtonItem done = null;
            done = new UIBarButtonItem (UIBarButtonSystemItem.Done, delegate {
                bc.Fetch ();
                done.Enabled = false;
                CheckAccount (
                    delegate (string errorMessage) { 
                        Util.PopNetworkActive ();
                        done.Enabled = true; 
                        if (null == errorMessage) {
                            var cred = new McCred ();
                            cred.Password = info.Password;
                            // TODO: Update the database
                            NavigationController.PopViewControllerAnimated (true);
                        } else {
                            dlg = new UIAlertView (Locale.GetText ("Login error"), errorMessage, null, Locale.GetText ("Close"));
                            dlg.Show ();
                        }
                    }
                );
            });
            NavigationItem.SetRightBarButtonItem (done, false);
        }
    }
}


