//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MailKit.Net.Imap;

namespace NachoCore.IMAP
{
    public class ImapDiscoverCommand : ImapCommand
    {
        public ImapDiscoverCommand (IBEContext beContext, ImapClient imap) : base (beContext, imap)
        {
            RedactProtocolLogFunc = RedactProtocolLog;
        }
        public string RedactProtocolLog (bool isRequest, string logData)
        {
            // Redaction is done in the base class, since it's more complicated than just string replacement
            return logData;
        }
        protected override Event ExecuteCommand ()
        {
            BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_AsAutoDComplete));
            return Event.Create ((uint)SmEvt.E.Success, "IMAPDISCOSUC");
        }
    }
}

