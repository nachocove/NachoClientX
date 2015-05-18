//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Xml.Linq;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public abstract class AsSmartCommand : AsCommand
    {
        protected McEmailMessage EmailMessage;

        public AsSmartCommand (IBEContext dataSource) : base (string.Empty, Xml.ComposeMail.Ns, dataSource)
        {
            // PendingSingle MarkDispatched & EmailMessage set by subclass.
        }

        public override Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation Sender)
        {
            if (14.0 <= Convert.ToDouble (BEContext.ProtocolState.AsProtocolVersion)) {
                return null;
            }

            return new Dictionary<string, string> () {
                { "ItemId", PendingSingle.ServerId },
                { "CollectionId", PendingSingle.ParentId },
                { "SaveInSent", "T" },
            };
        }

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            if (14.0 > Convert.ToDouble (BEContext.ProtocolState.AsProtocolVersion)) {
                return null;
            }
            var mimePath = EmailMessage.MimePath ();
            var length = new FileInfo (mimePath).Length;
            Timeout = new TimeSpan (0, 0, ((AsProtoControl)BEContext.ProtoControl).SyncStrategy.UploadTimeoutSecs (length));
            var smartMail = new XElement (m_ns + CommandName, 
                                new XElement (m_ns + Xml.ComposeMail.ClientId, EmailMessage.ClientId),
                                new XElement (m_ns + Xml.ComposeMail.Source,
                                    new XElement (m_ns + Xml.ComposeMail.FolderId, PendingSingle.ParentId),
                                    new XElement (m_ns + Xml.ComposeMail.ItemId, PendingSingle.ServerId)),
                                new XElement (m_ns + Xml.ComposeMail.SaveInSentItems),
                                new XElement (m_ns + Xml.ComposeMail.Mime, 
                    new XAttribute ("nacho-body-path", mimePath)));
            if (PendingSingle.Smart_OriginalEmailIsEmbedded) {
                smartMail.Add (new XElement (m_ns + Xml.ComposeMail.ReplaceMime));
            }
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (smartMail);
            return doc;
        }

        protected override Stream ToMime (AsHttpOperation Sender)
        {
            if (14.0 > Convert.ToDouble (BEContext.ProtocolState.AsProtocolVersion)) {
                long length;
                var stream = EmailMessage.ToMime (out length);
                Timeout = new TimeSpan (0, 0, ((AsProtoControl)BEContext.ProtoControl).SyncStrategy.UploadTimeoutSecs (length));
                return stream;
            }
            return null;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "SMARTCANCEL0");
            }
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, 
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSendSucceeded));
            });

            var sentFolder = McFolder.GetDefaultSentFolder (BEContext.Account.Id);
            if (null != sentFolder) {
                sentFolder.UpdateSet_AsSyncMetaToClientExpected (true);
            }
            return Event.Create ((uint)SmEvt.E.Success, "SESUCC");
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "SMARTCANCEL1");
            }
            PendingResolveApply ((pending) => {
                pending.ResolveAsHardFail (BEContext.ProtoControl, 
                    NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageSendFailed,
                        NcResult.WhyEnum.Unknown));
            });
            return Event.Create ((uint)SmEvt.E.HardFail, "SEFAIL", null, 
                string.Format ("Server sent non-empty response to SendMail: {0}", doc.ToString ()));
        }

        public override bool IsContentLarge (AsHttpOperation Sender)
        {
            return true;
        }
    }
}

