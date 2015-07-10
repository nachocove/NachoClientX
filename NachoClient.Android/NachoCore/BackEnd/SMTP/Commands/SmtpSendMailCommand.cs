//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using MimeKit;
using NachoCore.Utils;
using MailKit.Net.Smtp;

namespace NachoCore.SMTP
{
    public class SmtpSendMailCommand : SmtpCommand
    {
        public SmtpSendMailCommand (IBEContext beContext, NcSmtpClient smtp, McPending pending) : base (beContext, smtp)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            McEmailMessage EmailMessage = McAbstrObject.QueryById<McEmailMessage> (PendingSingle.ItemId);
            McBody body = McBody.QueryById<McBody> (EmailMessage.BodyId);
            MimeMessage mimeMessage = MimeHelpers.LoadMessage (body);
            var attachments = McAttachment.QueryByItemId (EmailMessage);
            if (attachments.Count > 0) {
                MimeHelpers.AddAttachments (mimeMessage, attachments);
            }

            try {
                Client.Send (mimeMessage, Cts.Token);
            } catch (SmtpCommandException ex) {
                Log.Info (Log.LOG_SMTP, "SmtpCommandException {0}", ex.Message);
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, 
                        NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageSendFailed,
                            NcResult.WhyEnum.ProtocolError));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "SMTPSENDHARD");
            }
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (
                    BEContext.ProtoControl,
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSendSucceeded));
            });
            if (McAccount.AccountServiceEnum.GoogleDefault != BEContext.ProtocolState.ImapServiceType) {
                McFolder sentFolder = McFolder.GetDefaultSentFolder (BEContext.Account.Id);
                if (null != sentFolder) {
                    sentFolder.Link (EmailMessage);
                    BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                }
            }
            return Event.Create ((uint)SmEvt.E.Success, "SMTPCONNSUC");
        }
    }
}

