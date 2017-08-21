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
        private const string ACCOUNT_ID_KEY = "CertAskFragment.accountId";
        private const string CERT_INFO_KEY = "CertAskFragment.certInfo";
        private const string CERT_COMMON_NAME_KEY = "CertAskFragment.certCommonName";
        private const string CERT_ORGANIZATION_KEY = "CertAskFragment.certOrganization";

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
                fragment.certInfo = fragment.GetString (Resource.String.cert_ask_error);
                fragment.certCommonName = "error";
                fragment.certOrganization = "error";
            } else {
                fragment.certInfo = CertificateHelper.FormatCertificateData (certToBeExamined);
                fragment.certCommonName = CertificateHelper.GetCommonName (certToBeExamined);
                fragment.certOrganization = CertificateHelper.GetOrganizationname (certToBeExamined);
            }

            return fragment;
        }

        public void SetListener (CertAskDelegate listener)
        {
            this.listener = listener;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            if (null != savedInstanceState) {
                accountId = savedInstanceState.GetInt (ACCOUNT_ID_KEY);
                certInfo = savedInstanceState.GetString (CERT_INFO_KEY);
                certCommonName = savedInstanceState.GetString (CERT_COMMON_NAME_KEY);
                certOrganization = savedInstanceState.GetString (CERT_ORGANIZATION_KEY);
            }
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (this.Activity);

            var inflater = this.Activity.LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.CertAskFragment, null);

            var certFmt = GetString (Resource.String.cert_ask_prompt);
            var certAskView = view.FindViewById<TextView> (Resource.Id.cert_ask);
            certAskView.Text = String.Format (certFmt, certCommonName, certOrganization);

            var certInfoView = view.FindViewById<TextView> (Resource.Id.cert_info);
            certInfoView.Text = certInfo;

            builder.SetView (view);
            builder.SetTitle (Resource.String.cert_ask_title);
            builder.SetPositiveButton (Resource.String.cert_ask_allow, (sender, args) => {
                listener.AcceptCertificate (accountId);
            });
            builder.SetNegativeButton (Resource.String.cert_ask_cancel, (sender, args) => {
                listener.DontAcceptCertificate (accountId);
            });

            return builder.Create ();
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            outState.PutInt (ACCOUNT_ID_KEY, accountId);
            outState.PutString (CERT_INFO_KEY, certInfo);
            outState.PutString (CERT_COMMON_NAME_KEY, certCommonName);
            outState.PutString (CERT_ORGANIZATION_KEY, certOrganization);
        }
    }
}

