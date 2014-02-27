//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public abstract class AsSmartCommand : AsCommand
    {
        protected McEmailMessage EmailMessage;

        public AsSmartCommand (IAsDataSource dataSource) : base (string.Empty, Xml.ComposeMail.Ns, dataSource)
        {
            // Update & EmailMessage set by subclass.
        }

        public override Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation Sender)
        {
            if (14.0 <= Convert.ToDouble (DataSource.ProtocolState.AsProtocolVersion)) {
                return null;
            }

            return new Dictionary<string, string> () {
                { "ItemId", Update.ServerId },
                { "CollectionId", Update.FolderServerId },
                { "SaveInSent", "T" },
            };
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            if (14.0 > Convert.ToDouble (DataSource.ProtocolState.AsProtocolVersion)) {
                return null;
            }

            var smartMail = new XElement (m_ns + CommandName, 
                                new XElement (m_ns + Xml.ComposeMail.ClientId, EmailMessage.ClientId),
                                new XElement (m_ns + Xml.ComposeMail.Source,
                                    new XElement (m_ns + Xml.ComposeMail.FolderId, Update.FolderServerId),
                                    new XElement (m_ns + Xml.ComposeMail.ItemId, Update.ServerId)),
                                new XElement (m_ns + Xml.ComposeMail.SaveInSentItems),
                                new XElement (m_ns + Xml.ComposeMail.Mime, GenerateMime ()));
            if (Update.OriginalEmailIsEmbedded) {
                smartMail.Add (new XElement (m_ns + Xml.ComposeMail.ReplaceMime));
            }
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (smartMail);
            Update.IsDispatched = true;
            Update.Update ();
            return doc;
        }

        public override string ToMime (AsHttpOperation Sender)
        {
            if (14.0 > Convert.ToDouble (DataSource.ProtocolState.AsProtocolVersion)) {
                return GenerateMime ();
            }
            return null;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
        {
            DataSource.Control.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSendSucceeded), new [] { Update.Token });
            EmailMessage.Delete ();
            Update.Delete ();
            return Event.Create ((uint)SmEvt.E.Success, "SESUCC");
        }
        // FIXME - need an OnFail callback for negative indication delivery.
        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            return Event.Create ((uint)SmEvt.E.HardFail, "SEFAIL", null, 
                string.Format ("Server sent non-empty response to SendMail: {0}", doc.ToString ()));
        }

        public override void Cancel ()
        {
            base.Cancel ();
            // FIXME - revert IsDispatched.
        }

        private string GenerateMime ()
        {
            return EmailMessage.ToMime ();
        }
    }
}

