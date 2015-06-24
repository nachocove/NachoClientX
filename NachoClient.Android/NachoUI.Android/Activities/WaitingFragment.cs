
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class WaitingFragment : Fragment
    {
        McAccount account;

        public WaitingFragment(McAccount account)
        {
            this.account = account;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.WaitingFragment, container, false);
            var tv = view.FindViewById<TextView> (Resource.Id.textview);
            tv.Text = "Waiting fragment";
            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        void SyncCompleted(int accountId)
        {
            var parent = (LaunchActivity)Activity;
            parent.WaitingFinished();
        }

        public void handleStatusEnums()
        {
            if (BackEndStateEnum.PostAutoDPostInboxSync == BackEnd.Instance.BackEndState (account.Id, account.AccountCapability)) {
                var parent = (LaunchActivity)Activity;
                parent.WaitingFinished();
            }
        }


        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            // Can't do anything without an account
            if (null == account) {
                return;
            }

            // Won't do anything if this isn't our account
            if ((null != s.Account) && (s.Account.Id != account.Id)) {
                return;
            }

            int accountId = account.Id;

            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Info_EmailMessageSetChanged Status Ind (AdvancedView)");
                SyncCompleted (accountId);
                return;
            }
            if (NcResult.SubKindEnum.Info_InboxPingStarted == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Info_InboxPingStarted Status Ind (AdvancedView)");
                SyncCompleted (accountId);
                return;
            }
            if (NcResult.SubKindEnum.Info_AsAutoDComplete == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Auto-D-Completed Status Ind (Advanced View)");
                handleStatusEnums ();
                return;
            }
            if (NcResult.SubKindEnum.Error_NetworkUnavailable == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Advanced Login status callback: Error_NetworkUnavailable");
                handleStatusEnums ();
                return;
            }
            if (NcResult.SubKindEnum.Error_ServerConfReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: ServerConfReq Status Ind (Adv. View)");
                handleStatusEnums ();
                return;
            }
            if (NcResult.SubKindEnum.Info_CredReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: CredReqCallback Status Ind (Adv. View)");
                handleStatusEnums ();
                return;
            }
            if (NcResult.SubKindEnum.Error_CertAskReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: CertAskCallback Status Ind");
                handleStatusEnums ();
                return;
            }
            if (NcResult.SubKindEnum.Info_NetworkStatus == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Advanced Login status callback: Info_NetworkStatus");
                handleStatusEnums ();
                return;
            }
        }

    }
}

