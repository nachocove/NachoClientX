// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class CertAskViewController : NcUIViewControllerNoLeaks, INachoCertificateResponderParent
    {
        McAccount account;
        McAccount.AccountCapabilityEnum capability;
        public INachoCertificateResponderParent CertificateDelegate;

        CertificateView certificateView;

        public CertAskViewController () : base ()
        {
        }

        public CertAskViewController (IntPtr handle) : base (handle)
        {
        }

        public void Setup (McAccount account, McAccount.AccountCapabilityEnum capability)
        {
            this.account = account;
            this.capability = capability;
        }

        protected override void CreateViewHierarchy ()
        {
            NavigationItem.Title = "Certificate";
            NavigationItem.HidesBackButton = true;

            View.BackgroundColor = A.Color_NachoGreen;

            INachoCertificateResponderParent owner = CertificateDelegate;
            if (owner == null) {
                owner = this;
            }
            certificateView = new CertificateView (View.Bounds, owner);
            certificateView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
            ViewFramer.Create (certificateView).Y (0);
            View.Add (certificateView);

            certificateView.SetCertificateInformation (account.Id, capability);
            certificateView.ShowView ();
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return true;
            }
        }

        // INachoCertificateResponderParent
        public void DontAcceptCertificate (int accountId)
        {
            NcApplication.Instance.CertAskResp (accountId, capability, false);
            LoginHelpers.UserInterventionStateChanged (accountId);
            NavigationController.PopViewController (true);
        }

        // INachoCertificateResponderParent
        public void AcceptCertificate (int accountId)
        {
            NcApplication.Instance.CertAskResp (accountId, capability, true);
            LoginHelpers.UserInterventionStateChanged (accountId);
            NavigationController.PopViewController (true);
        }

        protected override void OnKeyboardChanged ()
        {
            ConfigureAndLayout ();
        }

    }
}
