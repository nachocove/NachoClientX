//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsMeetingResponseCommand : AsCommand
    {
        public AsMeetingResponseCommand (IBEContext dataSource) : base (Xml.MeetingResp.MeetingResponse, Xml.MeetingResp.Ns, dataSource)
        {
            PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.CalRespond);
            PendingSingle.MarkDispached ();
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var meetingResp = new XElement (m_ns + Xml.MeetingResp.MeetingResponse,
                                  new XElement (m_ns + Xml.MeetingResp.Request,
                                      new XElement (m_ns + Xml.MeetingResp.UserResponse, PendingSingle.CalResponse),
                                      new XElement (m_ns + Xml.MeetingResp.CollectionId, PendingSingle.ParentId),
                                      new XElement (m_ns + Xml.MeetingResp.RequestId, PendingSingle.ServerId)));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (meetingResp);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            var xmlMeetingResp = doc.Root;
            var xmlResult = xmlMeetingResp.Element (m_ns + Xml.MeetingResp.Result);
            var xmlStatus = xmlResult.Element (m_ns + Xml.MeetingResp.Status);
            switch ((Xml.MeetingResp.StatusCode)Convert.ToUInt32 (xmlStatus.Value)) {
            case Xml.MeetingResp.StatusCode.Success_1:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_MeetingResponseSucceeded));
                });
                return Event.Create ((uint)SmEvt.E.Success, "FUPSUCCESS");

            case Xml.MeetingResp.StatusCode.InvalidMeetingRequest_2:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_MeetingResponseFailed,
                        NcResult.WhyEnum.BadOrMalformed));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "FUPFAIL1");

            default:
                PendingResolveApply ((pending) => {
                    PendingSingle.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_MeetingResponseFailed,
                        NcResult.WhyEnum.Unknown));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "FUPFAIL2");
            }
        }
    }
}

