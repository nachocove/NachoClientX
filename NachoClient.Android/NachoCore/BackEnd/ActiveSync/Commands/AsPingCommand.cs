using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsPingCommand : AsCommand
    {
        private IEnumerable<McFolder> FoldersInRequest;
        private uint HeartbeatInterval;

        public AsPingCommand (IBEContext dataSource, PingKit pingKit) : base (Xml.Ping.Ns, Xml.Ping.Ns, dataSource)
        {
            FoldersInRequest = pingKit.Folders;
            HeartbeatInterval = pingKit.MaxHeartbeatInterval;
            MaxTries = 1;
        }

        public override double TimeoutInSeconds {
            get {
                // Add a 10-second fudge so that orderly timeout doesn't look like a network failure.
                return (int)HeartbeatInterval + 10;
            }
        }

        public override bool DoSendPolicyKey (AsHttpOperation Sender)
        {
            return false;
        }

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var xFolders = new XElement (m_ns + Xml.Ping.Folders);

            foreach (var folder in FoldersInRequest) {
                if (Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == folder.Type) {
                    BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_InboxPingStarted));
                }
                xFolders.Add (new XElement (m_ns + Xml.Ping.Folder,
                    new XElement (m_ns + Xml.Ping.Id, folder.ServerId),
                    new XElement (m_ns + Xml.Ping.Class, Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (folder.Type))));
            }
            var ping = new XElement (m_ns + Xml.Ping.Ns);
            ping.Add (new XElement (m_ns + Xml.Ping.HeartbeatInterval, HeartbeatInterval.ToString ()));
            ping.Add (xFolders);
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (ping);
            return doc;
        }

        private void MarkFoldersPinged ()
        {
            foreach (var iterFolder in FoldersInRequest) {
                iterFolder.UpdateSet_AsSyncLastPing (DateTime.UtcNow);
            }
            var protocolState = BEContext.ProtocolState;
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.LastPing = DateTime.UtcNow;
                return true;
            });
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            McProtocolState protocolState;
            // NOTE: Important to remember that in this context, SmEvt.E.Success means to do another long-poll.
            string statusString = doc.Root.Element (m_ns + Xml.Ping.Status).Value;
            switch ((Xml.Ping.StatusCode)Convert.ToUInt32 (statusString)) {

            case Xml.Ping.StatusCode.NoChanges_1:
                MarkFoldersPinged ();
                return Event.Create ((uint)SmEvt.E.Success, "PINGNOCHG");
            
            case Xml.Ping.StatusCode.Changes_2:
                MarkFoldersPinged ();
                var folders = doc.Root.Element (m_ns + Xml.Ping.Folders).Elements (m_ns + Xml.Ping.Folder);
                foreach (var xmlFolder in folders) {
                    var folder = NcModel.Instance.Db.Table<McFolder> ().
                        Where (rec => AccountId == rec.AccountId && xmlFolder.Value == rec.ServerId).
                        Single ();
                    folder = folder.UpdateSet_AsSyncMetaToClientExpected (true);
                }
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "PINGRESYNC");
            
            case Xml.Ping.StatusCode.MissingParams_3:
            case Xml.Ping.StatusCode.SyntaxError_4:
                return Event.Create ((uint)SmEvt.E.HardFail, "PINGHARD0", null, "Xml.Ping.StatusCode.MissingParams/SyntaxError");

            case Xml.Ping.StatusCode.BadHeartbeat_5:
                protocolState = BEContext.ProtocolState;
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.HeartbeatInterval = uint.Parse (doc.Root.Element (m_ns + Xml.Ping.HeartbeatInterval).Value);
                    return true;
                });
                return Event.Create ((uint)SmEvt.E.Success, "PINGBADH");

            case Xml.Ping.StatusCode.TooManyFolders_6:
                protocolState = BEContext.ProtocolState;
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.MaxFolders = uint.Parse (doc.Root.Element (m_ns + Xml.Ping.MaxFolders).Value);
                    return true;
                });
                return Event.Create ((uint)SmEvt.E.Success, "PINGTMF");

            case Xml.Ping.StatusCode.NeedFolderSync_7:
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "PINGNFS");
            
            case Xml.Ping.StatusCode.ServerError_8:
                return Event.Create ((uint)SmEvt.E.TempFail, "PINGSE", null, "Xml.Ping.StatusCode.ServerError");

            default:
                Log.Error (Log.LOG_AS, "AsPingCommand ProcessResponse UNHANDLED status {0}", statusString);
                return Event.Create ((uint)SmEvt.E.HardFail, "PINGHARD1");
            }
        }

        // PushAssist support.
        public string PushAssistRequestUrl ()
        {
            Op = new AsHttpOperation (CommandName, this, BEContext);
            return ServerUri (Op).ToString ();
        }

        public NcHttpHeaders PushAssistRequestHeaders ()
        {
            Op = new AsHttpOperation (CommandName, this, BEContext);
            NcHttpRequest request;
            if (!Op.CreateHttpRequest (out request, CancellationToken.None)) {
                return null;
            }
            var headers = request.Headers;
            request.Dispose ();
            return headers;
        }

        public byte[] PushAssistRequestData ()
        {
            Op = new AsHttpOperation (CommandName, this, BEContext);
            return ToXDocument (Op).ToWbxml (doFiltering: false);
        }

        public byte[] PushAssistNoChangeResponseData ()
        {
            var response = ToEmptyXDocument ();
            response.Add (new XElement (m_ns + Xml.Ping.Ns,
                new XElement (m_ns + Xml.Ping.Status, "1")));
            return response.ToWbxml (doFiltering: false);
        }
    }
}

