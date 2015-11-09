//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{

    public interface CertAskDelegate
    {
        void AcceptCertificate (int accountId);

        void DontAcceptCertificate (int accountId);
    }

    public class CertAskFragment : DialogFragment
    {
        protected int accountId;
        protected string certInfo;
        protected string certCommonName;
        protected string certOrganization;

        protected CertAskDelegate listener;

        public static CertAskFragment newInstance (int accountId, NachoCore.Model.McAccount.AccountCapabilityEnum capability, CertAskDelegate listener)
        {
            var fragment = new CertAskFragment ();

            fragment.accountId = accountId;
            fragment.listener = listener;

            var certToBeExamined = BackEnd.Instance.ServerCertToBeExamined (accountId, capability);
            if (null == certToBeExamined) {
                fragment.certInfo = "Unable to find certificate to be examined.";
                fragment.certCommonName = "error";
                fragment.certOrganization = "error";
            } else {
                fragment.certInfo = CertificateHelper.FormatCertificateData (certToBeExamined);
                fragment.certCommonName = CertificateHelper.GetCommonName (certToBeExamined);
                fragment.certOrganization = CertificateHelper.GetOrganizationname (certToBeExamined);
            }

            return fragment;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (this.Activity);

            var inflater = this.Activity.LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.CertAskFragment, null);

            var certFmt = GetString (Resource.String.cert_ask);
            var certAskView = view.FindViewById<TextView> (Resource.Id.cert_ask);
            certAskView.Text = String.Format (certFmt, certCommonName, certOrganization);

            var certInfoView = view.FindViewById<TextView> (Resource.Id.cert_info);
            certInfoView.Text = certInfo;

            builder.SetView (view);
            builder.SetTitle (Resource.String.security_warning);
            builder.SetPositiveButton (Resource.String.allow, (sender, args) => {
                listener.AcceptCertificate (accountId);
            });
            builder.SetNegativeButton (Resource.String.cancel, (sender, args) => {
                listener.DontAcceptCertificate (accountId);
            });

            return builder.Create ();
        }
    }
}

