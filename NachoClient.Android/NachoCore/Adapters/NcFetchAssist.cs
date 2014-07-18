//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    // Used to manage fetch of an on-server object on behalf of the UI for immediate use.
    public class NcFetchAssist
    {
        private delegate string BEActionFunc (int accountId, int subjectId);

        private BEActionFunc BEAction;
        private NcResult.SubKindEnum SubKindMatchS, SubKindMatchF;
        private string Token;
        private McAbstrObjectPerAcc Subject;
        private Action<McAbstrObjectPerAcc, NcResult> OnSuccess;
        private Action<McAbstrObjectPerAcc, NcResult> OnFailure;

        public NcFetchAssist (McAbstrObjectPerAcc myObj, 
                              Action<McAbstrObjectPerAcc, NcResult> onSuccess,
                              Action<McAbstrObjectPerAcc, NcResult> onFailure)
        {
            Subject = myObj;
            // TODO: implement other classes as we need them.
            if (Subject is McEmailMessage) {
                BEAction = BackEnd.Instance.DnldEmailBodyCmd;
                SubKindMatchS = NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded;
                SubKindMatchF = NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed;
            } else if (Subject is McAttachment) {
                BEAction = BackEnd.Instance.DnldAttCmd;
                SubKindMatchS = NcResult.SubKindEnum.Info_AttDownloadUpdate;
                SubKindMatchF = NcResult.SubKindEnum.Error_AttDownloadFailed;
            } else {
                NcAssert.True (false, string.Format ("NcFetchAssist: {0} not yet implemented.", myObj.ClassName ()));
            }
            OnSuccess = onSuccess;
            OnFailure = onFailure;
        }

        public void Cancel ()
        {
            NcApplication.Instance.StatusIndEvent -= StatusIndReceiver;
            if (null != Token) {
                BackEnd.Instance.Cancel (Subject.AccountId, Token);
            }
        }


        public NcResult Execute ()
        {
            if (NcCommStatus.Instance.Status == NachoPlatform.NetStatusStatusEnum.Down) {
                return NcResult.Error (NcResult.SubKindEnum.Error_NetworkUnavailable);
            }
            NcApplication.Instance.StatusIndEvent += StatusIndReceiver;
            Token = BEAction (Subject.AccountId, Subject.Id);
            if (null == Token) {
                return NcResult.Error (NcResult.SubKindEnum.Error_InvalidParameter);
            }
            return NcResult.OK ();
        }

        private bool TokenMatch (string token)
        {
            return token == Token;
        }

        private void StatusIndReceiver (object sender, EventArgs ea)
        {
            StatusIndEventArgs siea = (StatusIndEventArgs)ea;
            if (siea.Status.SubKind == SubKindMatchS && Array.Exists (siea.Tokens, TokenMatch)) {
                NcApplication.Instance.StatusIndEvent -= StatusIndReceiver;
                OnSuccess (Subject, siea.Status);
            } else if (siea.Status.SubKind == SubKindMatchF && Array.Exists (siea.Tokens, TokenMatch)) {
                NcApplication.Instance.StatusIndEvent -= StatusIndReceiver;
                OnFailure (Subject, siea.Status);
            }
        }
    }
}
