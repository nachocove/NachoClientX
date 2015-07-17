//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MimeKit;
using MailKit;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Model;
using System.IO;
using System.Text;
using System;

namespace NachoCore.IMAP
{
    public class ImapFetchBodyCommand : ImapFetchCommand
    {
        public ImapFetchBodyCommand (IBEContext beContext, NcImapClient imap, McPending pending) : base (beContext, imap)
        {
            pending.MarkDispached ();
            PendingSingle = pending;
        }

        protected override Event ExecuteCommand ()
        {
            NcResult result = null;
            result = ProcessPending (PendingSingle);
            if (result.isInfo ()) {
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, result);
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPBDYSUCC");
            } else {
                NcAssert.True (result.isError ());
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, result);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPBDYHRD0");
            }
        }

        private NcResult ProcessPending (McPending pending)
        {
            McPending.Operations op;
            if (null == pending) {
                // If there is no pending, then we are doing an email prefetch.
                op = McPending.Operations.EmailBodyDownload;
            } else {
                op = pending.Operation;
            }
            switch (op) {
            case McPending.Operations.EmailBodyDownload:
                return FetchOneBody (pending);

            default:
                NcAssert.True (false, string.Format ("ItemOperations: inappropriate McPending Operation {0}", pending.Operation));
                break;
            }
            return NcResult.Error ("Unknown operation");
        }

        private NcResult FetchOneBody (McPending pending)
        {
            McEmailMessage email = McAbstrItem.QueryByServerId<McEmailMessage> (BEContext.Account.Id, pending.ServerId);
            if (null == email) {
                Log.Error (Log.LOG_IMAP, "Could not find email for {0}", pending.ServerId);
                return NcResult.Error ("Unknown email ServerId");
            }

            NcResult result;
            McFolder folder = McFolder.QueryByServerId (BEContext.Account.Id, pending.ParentId);
            var mailKitFolder = GetOpenMailkitFolder (folder);

            MimeMessage imapbody = null;
            try {
                imapbody = mailKitFolder.GetMessage (ImapProtoControl.ImapMessageUid(pending.ServerId), Cts.Token);
            } catch (ImapCommandException ex) {
                Log.Warn (Log.LOG_IMAP, "ImapCommandException: {0}", ex.Message);
                // TODO Need to narrow this down. Pull in latest MailKit and make it compile.
                imapbody = null;
            }
            if (null == imapbody) {
                // the message doesn't exist. Delete it locally.
                Log.Warn (Log.LOG_IMAP, "ImapFetchBodyCommand: no message found. Deleting local copy");
                email.Delete ();
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                result = NcResult.Error ("No Body found");
            } else {
                McAbstrFileDesc.BodyTypeEnum bodyType;
                // FIXME Getting the 'body' string is inefficient and wasteful.
                //   Perhaps use the WriteTo method on the Body, write to a file,
                //   then open the file and pass that stream to UpdateData/InsertFile?
                string bodyAsString;
                if (imapbody.Body.ContentType.Matches ("multipart", "*")) {
                    bodyType = McAbstrFileDesc.BodyTypeEnum.MIME_4;
                    bodyAsString = imapbody.Body.ToString ();
                } else if (imapbody.Body.ContentType.Matches ("text", "*")) {
                    if (imapbody.Body.ContentType.Matches ("text", "html")) {
                        bodyType = McAbstrFileDesc.BodyTypeEnum.HTML_2;
                        bodyAsString = imapbody.HtmlBody;
                    } else if (imapbody.Body.ContentType.Matches ("text", "plain")) {
                        bodyType = McAbstrFileDesc.BodyTypeEnum.PlainText_1;
                        bodyAsString = imapbody.TextBody;
                    } else {
                        Log.Error (Log.LOG_IMAP, "Unhandled text subtype {0}", imapbody.Body.ContentType.MediaSubtype);
                        return NcResult.Error ("Unhandled text subtype");
                    }
                } else {
                    Log.Error (Log.LOG_IMAP, "Unhandled mime subtype {0}", imapbody.Body.ContentType.ToString ());
                    return NcResult.Error ("Unhandled mimetype subtype");
                }

                McBody body;
                if (0 == email.BodyId) {
                    body = McBody.InsertFile (pending.AccountId, bodyType, bodyAsString); 
                    email.BodyId = body.Id;
                } else {
                    body = McBody.QueryById<McBody> (email.BodyId);
                    body.UpdateData (bodyAsString);
                }
                body.BodyType = bodyType;
                body.Truncated = false;
                body.FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete;
                body.FileSize = bodyAsString.Length;
                body.FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Actual;
                body.Update ();
                result = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded);
                email.Update ();
                if (string.IsNullOrEmpty (email.BodyPreview)) {
                    var preview = BodyToPreview (body);
                    if (!string.IsNullOrEmpty (preview)) {
                        email = email.UpdateWithOCApply<McEmailMessage> ((record) => {
                            var target = (McEmailMessage)record;
                            target.BodyPreview = preview;
                            return true;
                        });
                        StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                    }
                }
            }
            return result;
        }

        private static string BodyToPreview (McBody body, int previewLength = 500)
        {
            string preview = string.Empty;
            using (var bodyFile = new FileStream (body.GetFilePath (), FileMode.Open, FileAccess.Read)) {
                switch (body.BodyType) {
                case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                    // TODO This reads the entire file into memory. Perhaps not the best idea?
                    preview = Html2Text (bodyFile);
                    break;

                case McAbstrFileDesc.BodyTypeEnum.PlainText_1:
                    byte[] pbytes = new byte[previewLength];
                    bodyFile.Read (pbytes, 0, previewLength);
                    preview = Encoding.UTF8.GetString (pbytes);
                    break;

                case McAbstrFileDesc.BodyTypeEnum.MIME_4:
                    // TODO This reads the entire file into memory. Perhaps not the best idea?
                    MimeMessage mime = MimeHelpers.LoadMessage (body);
                    string html;
                    string text;
                    if (MimeHelpers.FindText (mime, out html, out text)) {
                        if (!string.IsNullOrEmpty (text)) {
                            preview = text;
                        } else if (!string.IsNullOrEmpty (html)) {
                            preview = Html2Text (html);
                        }
                    }
                    break;
                }
            }
            if (preview.Length > previewLength) {
                preview = preview.Substring (0, previewLength);
            }
            return preview;
        }
    }
}
