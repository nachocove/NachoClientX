using System;
using MonoTouch.Foundation;
using NachoCore.Model;
using NachoCore.Brain;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public interface INachoMessageComposer
    {
        void SetQRType (NcQuickResponse.QRTypeEnum QRType);
        void SetMailToUrl (string urlString);
        void SetEmailPresetFields (NcEmailAddress toAddress = null, string subject = null, string emailTemplate = null, List<McAttachment> attachmentList = null, bool isQR = false);
        void SetDraftPresetFields (List<NcEmailAddress> recipients = null, string subject = null, string emailTemplate = null, List<McAttachment> attachmentList = null);
        void SetAction (McEmailMessageThread thread, string actionString);
    }
}
