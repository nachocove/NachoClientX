//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using MimeKit;
using NachoCore.Utils;
using MailKit.Net.Smtp;
using System.IO;

namespace NachoCore.SMTP
{
    public class SmtpSendBaseCommand : SmtpCommand
    {
        protected McEmailMessage EmailMessage;

        public SmtpSendBaseCommand (IBEContext beContext, NcSmtpClient smtp, McPending pending) : base (beContext, smtp)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
            EmailMessage = McAbstrObject.QueryById<McEmailMessage> (PendingSingle.ItemId);
        }

        protected virtual MimeMessage CreateMimeMessage()
        {
            // TODO Deal with memory issues, i.e. don't read everything into memory
            long length;
            var BodyParser = new MimeParser (EmailMessage.ToMime (out length), true);
            MimeMessage message = BodyParser.ParseMessage ();
            switch (PendingSingle.Operation) {
            case McPending.Operations.EmailForward:
            case McPending.Operations.EmailReply:
                if (!PendingSingle.Smart_OriginalEmailIsEmbedded) {
                    McEmailMessage referencedEmail = McEmailMessage.QueryByServerId<McEmailMessage> (EmailMessage.AccountId, PendingSingle.ServerId);
                    var ReferencedParser = new MimeParser (referencedEmail.ToMime (out length), true);
                    MimeMessage referencedMime = ReferencedParser.ParseMessage ();
                    Multipart mixed = new Multipart ("mixed");
                    if (null != message.Body) {
                        mixed.Add (message.Body);
                    }
                    mixed.Add (new MessagePart { Message = referencedMime});
                    message.Body = mixed;
                }
                break;

            case McPending.Operations.EmailSend:
                break;

            default:
                NcAssert.CaseError (string.Format ("Unknown McPending.Operations: {0}", PendingSingle.Operation));
                break;
            }
            return message;
        }

        protected override Event ExecuteCommand ()
        {
            var mimeMessage = CreateMimeMessage ();
            Client.Send (mimeMessage, Cts.Token);
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (
                    BEContext.ProtoControl,
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSendSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "SMTPCONNSUC");
        }
    }

    public class SmtpSendMailCommand : SmtpSendBaseCommand
    {
        public SmtpSendMailCommand (IBEContext beContext, NcSmtpClient smtp, McPending pending) : base (beContext, smtp, pending)
        {
        }
    }

    public class SmtpForwardMailCommand : SmtpSendBaseCommand
    {
        public SmtpForwardMailCommand (IBEContext beContext, NcSmtpClient smtp, McPending pending) : base (beContext, smtp, pending)
        {
        }
    }

    public class SmtpReplyMailCommand : SmtpSendBaseCommand
    {
        public SmtpReplyMailCommand (IBEContext beContext, NcSmtpClient smtp, McPending pending) : base (beContext, smtp, pending)
        {
        }
    }

}

