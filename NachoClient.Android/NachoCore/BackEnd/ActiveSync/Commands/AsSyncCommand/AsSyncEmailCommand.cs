//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using MimeKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;

namespace NachoCore.ActiveSync
{
    public partial class AsSyncCommand : AsCommand
    {
        const string KEmailCheck = "SSAOCE_EmailCheck";
        const string KEmailParse = "SSAOCE_EmailParse";
        const string KEmailInsert = "SSAOCE_EmailInsert";
        const string KEmailUpdate = "SSAOCE_EmailUpdate";
        const string KEmailDelete = "SSAOCE_EmailDelete";
        const string KEmailAddress = "SSAOCE_EmailAddress";
        const string KEmailGlean = "SSAOCE_EmailGlean";
        const string KEmailLink = "SSAOCE_EmailLink";
        const string KEmailAtt = "SSAOCE_EmailAtt";

        public static McEmailMessage ServerSaysAddOrChangeEmail (XElement command, McFolder folder)
        {
            // TODO if these were permanent, we'd want to do this once.
            NcCapture.AddKind (KEmailParse);
            NcCapture.AddKind (KEmailCheck);
            NcCapture.AddKind (KEmailInsert);
            NcCapture.AddKind (KEmailUpdate);
            NcCapture.AddKind (KEmailDelete);
            NcCapture.AddKind (KEmailAddress);
            NcCapture.AddKind (KEmailGlean);
            NcCapture.AddKind (KEmailLink);
            NcCapture.AddKind (KEmailAtt);

            var xmlServerId = command.Element (Ns + Xml.AirSync.ServerId);
            if (null == xmlServerId || null == xmlServerId.Value || string.Empty == xmlServerId.Value) {
                Log.Error (Log.LOG_AS, "ServerSaysAddOrChangeEmail: No ServerId present.");
                return null;
            }
            var capture = NcCapture.CreateAndStart (KEmailCheck);
            // If the server attempts to overwrite, delete the pre-existing record first.
            var eMsg = McEmailMessage.QueryByServerId<McEmailMessage> (folder.AccountId, xmlServerId.Value);
            capture.Stop ();
            if (Xml.AirSync.Add == command.Name.LocalName && null != eMsg) {
                capture = NcCapture.CreateAndStart (KEmailDelete);
                eMsg.Delete ();
                capture.Stop ();
                eMsg = null;
            }

            McEmailMessage emailMessage = null;
            AsHelpers aHelp = new AsHelpers ();
            try {
                capture = NcCapture.CreateAndStart (KEmailParse);
                var r = aHelp.ParseEmail (Ns, command, folder);
                capture.Stop ();
                emailMessage = r.GetValue<McEmailMessage> ();
            } catch (Exception ex) {
                Log.Error (Log.LOG_AS, "ServerSaysAddOrChangeEmail: Exception parsing: {0}", ex.ToString ());
                if (null == emailMessage || null == emailMessage.ServerId || string.Empty == emailMessage.ServerId) {
                    emailMessage = new McEmailMessage () {
                        ServerId = xmlServerId.Value,
                    };
                }
                emailMessage.IsIncomplete = true;
            }

            capture = NcCapture.CreateAndStart (KEmailAddress);
            McEmailAddress fromEmailAddress;
            if (McEmailAddress.Get (folder.AccountId, emailMessage.From, out fromEmailAddress)) {
                emailMessage.FromEmailAddressId = fromEmailAddress.Id;
                emailMessage.cachedFromLetters = EmailHelper.Initials(emailMessage.From);
                emailMessage.cachedFromColor = fromEmailAddress.ColorIndex;
            } else {
                emailMessage.FromEmailAddressId = 0;
                emailMessage.cachedFromLetters = "";
                emailMessage.cachedFromColor = 1;
            }

            emailMessage.SenderEmailAddressId = McEmailAddress.Get (folder.AccountId, emailMessage.Sender);
            emailMessage.ToEmailAddressId = McEmailAddress.GetList (folder.AccountId, emailMessage.To);
            emailMessage.CcEmailAddressId = McEmailAddress.GetList (folder.AccountId, emailMessage.Cc);
            capture.Stop ();
            NcModel.Instance.RunInTransaction (() => {
                if ((0 != emailMessage.FromEmailAddressId) || (0 < emailMessage.ToEmailAddressId.Count)) {
                    capture = NcCapture.CreateAndStart (KEmailGlean);
                    NcContactGleaner.GleanContactsHeaderPart1 (emailMessage);
                    capture.Stop ();
                }
                bool justCreated = false;
                if (null == eMsg) {
                    justCreated = true;
                    emailMessage.AccountId = folder.AccountId;
                }
                if (justCreated) {
                    capture = NcCapture.CreateAndStart (KEmailInsert);
                    emailMessage.Insert ();
                    capture.Stop ();
                    capture = NcCapture.CreateAndStart (KEmailLink);
                    folder.Link (emailMessage);
                    capture.Stop ();
                    capture = NcCapture.CreateAndStart (KEmailAtt);
                    aHelp.InsertAttachments (emailMessage);
                    capture.Stop ();
                } else {
                    emailMessage.AccountId = folder.AccountId;
                    emailMessage.Id = eMsg.Id;
                    capture = NcCapture.CreateAndStart (KEmailLink);
                    folder.UpdateLink (emailMessage);
                    capture.Stop ();
                    capture = NcCapture.CreateAndStart (KEmailUpdate);
                    emailMessage.Update ();
                    capture.Stop ();
                }
            });
            return emailMessage;
        }
    }
}
