//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoPlatform;

namespace NachoCore.Utils
{
    public class ErrorHelper
    {
        public static bool ExtractErrorString (NcResult nr, out string errorString)
        {
            return ErrorStringForSubkind (nr.SubKind, out errorString);
        }

        public static bool ErrorStringForSubkind (NcResult.SubKindEnum SubKind, out string errorString)
        {
            string message = null;
            switch (SubKind) {
            case NcResult.SubKindEnum.Error_NetworkUnavailable:
                message = Strings.Instance.ErrorNetworkUnavailable;
                break;
            case NcResult.SubKindEnum.Error_NoSpace:
                message = Strings.Instance.ErrorOutOfSpace;
                break;
            case NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed:
                message = Strings.Instance.ErrorMessageDownloadFailed;
                break;
            case NcResult.SubKindEnum.Error_CalendarBodyDownloadFailed:
                message = Strings.Instance.ErrorCalendarDownloadFailed;
                break;
            case NcResult.SubKindEnum.Error_AttDownloadFailed:
                message = Strings.Instance.ErrorAttachmentDownloadFailed;
                break;
            case NcResult.SubKindEnum.Error_AuthFailBlocked:
                message = Strings.Instance.ErrorAuthFail;
                break;
            case NcResult.SubKindEnum.Error_AuthFailPasswordExpired:
                message = Strings.Instance.ErrorPasswordExpired;
                break;
            case NcResult.SubKindEnum.Error_CredWait:
                message = Strings.Instance.ErrorPasswordUpdate;
                break;
            case NcResult.SubKindEnum.Info_ServiceUnavailable:
                message = Strings.Instance.ErrorServiceUnavailable;
                break;
            }
            errorString = message;
            return (null != message);
        }
    }
}

