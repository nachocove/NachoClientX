//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsMoveItemsCommand : AsCommand
    {
        public AsMoveItemsCommand (IAsDataSource dataSource) : base (Xml.Mov.MoveItems, Xml.Mov.Ns, dataSource)
        {
            Update = NextPending (McPending.Operations.EmailMove);
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            // FIXME - pack in multiple move ops.
            var move = new XElement (m_ns + Xml.Mov.MoveItems,
                           new XElement (m_ns + Xml.Mov.Move,
                               new XElement (m_ns + Xml.Mov.SrcMsgId, Update.EmailMessageServerId),
                               new XElement (m_ns + Xml.Mov.SrcFldId, Update.FolderServerId),
                               new XElement (m_ns + Xml.Mov.DstFldId, Update.DestFolderServerId)));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (move);
            Update.IsDispatched = true;
            Update.Update ();
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            // FIXME send indication of success/failure.
            // Right now, there will only be 1 Response because we issue 1 at a time.
            var xmlResponse = doc.Root.Element (m_ns + Xml.Mov.Response);
            switch ((Xml.Mov.StatusCode)Convert.ToUInt32 (xmlResponse.Element (m_ns + Xml.Mov.Status).Value)) {
            case Xml.Mov.StatusCode.Success:
                var xmlDstMsgId = xmlResponse.Element (m_ns + Xml.Mov.DstMsgId);
                if (null != xmlDstMsgId) {
                    var SrcMsgId = xmlResponse.Element (m_ns + Xml.Mov.SrcMsgId).Value;
                    var emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().SingleOrDefault (x => x.Id == Update.EmailMessageId);
                    if (null != emailMessage) {
                        emailMessage.ServerId = xmlDstMsgId.Value;
                        emailMessage.Update ();
                    }
                }
                DataSource.Control.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSendSucceeded),
                    new [] { Update.Token });
                Update.Delete ();
                return Event.Create ((uint)SmEvt.E.Success, "MVSUCCESS");

            case Xml.Mov.StatusCode.InvalidSrc:
            case Xml.Mov.StatusCode.InvalidDest:
                DataSource.Control.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageSendFailed),
                    new [] { Update.Token });
                Update.Delete ();
                return Event.Create ((uint)(uint)AsProtoControl.CtlEvt.E.ReFSync, "MVFSYNC");

            case Xml.Mov.StatusCode.SrcDestSame:
            case Xml.Mov.StatusCode.ClobberOrMulti:
                DataSource.Control.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageSendFailed),
                    new [] { Update.Token });
                Update.Delete ();
                return Event.Create ((uint)SmEvt.E.Success, "MVGRINF");

            case Xml.Mov.StatusCode.Locked:
                // We don't delete the update, no indication - we will retry.
                Update.IsDispatched = true;
                Update.Update ();
                return Event.Create ((uint)(uint)AsProtoControl.AsEvt.E.ReSync, "MVSYNC");
            
            default:
                DataSource.Control.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageSendFailed),
                    new [] { Update.Token });
                return Event.Create ((uint)SmEvt.E.Success, "MVUNKSTATUS");
            }
        }
    }
}

