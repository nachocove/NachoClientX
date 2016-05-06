//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsMeetingResponseCommand : AsCommand
    {
        public AsMeetingResponseCommand (IBEContext dataSource, McPending pending) : 
            base (Xml.MeetingResp.MeetingResponse, Xml.MeetingResp.Ns, dataSource)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispatched ();
        }

        protected override bool RequiresPending ()
        {
            return true;
        }

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var request = new XElement (m_ns + Xml.MeetingResp.Request,
                              new XElement (m_ns + Xml.MeetingResp.UserResponse, (uint)PendingSingle.CalResponse),
                              new XElement (m_ns + Xml.MeetingResp.CollectionId, PendingSingle.ParentId),
                              new XElement (m_ns + Xml.MeetingResp.RequestId, PendingSingle.ServerId));
            if (DateTime.MinValue != PendingSingle.CalResponseInstance) {
                if ("14.1" == BEContext.ProtocolState.AsProtocolVersion) {
                    request.Add (new XElement (m_ns + Xml.MeetingResp.InstanceId, 
                        PendingSingle.CalResponseInstance.ToAsUtcString ()));
                } else {
                    Log.Error (Log.LOG_AS, "{0}:InstanceId specified without protocol support.", CmdNameWithAccount);
                }
            }
            var meetingResp = new XElement (m_ns + Xml.MeetingResp.MeetingResponse, request);
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (meetingResp);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "MRESPCANCEL");
            }
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
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_MeetingResponseFailed,
                        NcResult.WhyEnum.Unknown));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "FUPFAIL2");
            }
        }
    }
}

