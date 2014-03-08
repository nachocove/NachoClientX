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
        public AsMeetingResponseCommand (IAsDataSource dataSource) : base (Xml.MeetingResp.MeetingResponse, Xml.MeetingResp.Ns, dataSource)
        {
            Update = NextPending (McPending.Operations.CalRespond);
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var meetingResp = new XElement (m_ns + Xml.MeetingResp.MeetingResponse,
                                  new XElement (m_ns + Xml.MeetingResp.Request,
                                      new XElement (m_ns + Xml.MeetingResp.UserResponse, Update.CalResponse),
                                      new XElement (m_ns + Xml.MeetingResp.CollectionId, Update.FolderServerId),
                                      new XElement (m_ns + Xml.MeetingResp.RequestId, Update.ServerId)));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (meetingResp);
            Update.IsDispatched = true;
            Update.Update ();
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            var xmlMeetingResp = doc.Root;
            switch ((Xml.MeetingResp.StatusCode)Convert.ToUInt32 (xmlMeetingResp.Element (m_ns + Xml.MeetingResp.Status).Value)) {
            case Xml.MeetingResp.StatusCode.Success:
                Update.Delete ();
                return Event.Create ((uint)SmEvt.E.Success, "FUPSUCCESS");

            case Xml.MeetingResp.StatusCode.InvalidMeetingRequest:
                // FIXME - status-ind required.
            default:
                Update.Delete ();
                return Event.Create ((uint)SmEvt.E.HardFail, "FUPFAIL");
            }
        }
    }
}

