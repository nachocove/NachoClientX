//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

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
                message = "The network is unavailable.";
                break;
            case NcResult.SubKindEnum.Error_NoSpace:
                message = "Your device is out of space.";
                break;
            case NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed:
                message = "Message download failed.";
                break;
            case NcResult.SubKindEnum.Error_CalendarBodyDownloadFailed:
                message = "Calendar body download failed.";
                break;
            case NcResult.SubKindEnum.Error_AttDownloadFailed:
                message = "Attachment download failed.";
                break;
            case NcResult.SubKindEnum.Error_AuthFailBlocked:
                message = "Authorization failed.";
                break;
            case NcResult.SubKindEnum.Error_AuthFailPasswordExpired:
                message = "Your password has expired.";
                break;
            case NcResult.SubKindEnum.Error_CredWait:
                message = "Your password may need to be updated.";
                break;
            case NcResult.SubKindEnum.Info_ServiceUnavailable:
                message = "Service unavailable.";
                break;
            }
            errorString = message;
            return (null != message);
        }
    }
}

