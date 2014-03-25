using System;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsPingCommand : AsCommand
    {
        private bool m_hitMaxFolders = false;

        public AsPingCommand (IBEContext dataSource) : base (Xml.Ping.Ns, Xml.Ping.Ns, dataSource)
        {
            // Add a 10-second fudge so that orderly timeout doesn't look like a network failure.
            Timeout = new TimeSpan (0, 0, (int)BEContext.ProtocolState.HeartbeatInterval + 10);
        }

        public override bool DoSendPolicyKey (AsHttpOperation Sender)
        {
            return false;
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            uint foldersLeft = BEContext.ProtocolState.MaxFolders;
            var xFolders = new XElement (m_ns + Xml.Ping.Folders);
            var folders = BackEnd.Instance.Db.Table<McFolder> ().Where (x => x.AccountId == BEContext.Account.Id &&
                          false == x.IsClientOwned &&
                          ((uint)Xml.FolderHierarchy.TypeCode.DefaultContacts_9 == x.Type
                          || (uint)Xml.FolderHierarchy.TypeCode.DefaultCal_8 == x.Type
                          || (uint)Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == x.Type
                          || (uint)Xml.FolderHierarchy.TypeCode.DefaultDrafts_3 == x.Type
                          || (uint)Xml.FolderHierarchy.TypeCode.DefaultSent_5 == x.Type
                          || (uint)Xml.FolderHierarchy.TypeCode.DefaultOutbox_6 == x.Type
                          || (uint)Xml.FolderHierarchy.TypeCode.DefaultDeleted_4 == x.Type
                          ));

            foreach (var folder in folders) {
                xFolders.Add (new XElement (m_ns + Xml.Ping.Folder,
                    new XElement (m_ns + Xml.Ping.Id, folder.ServerId),
                    new XElement (m_ns + Xml.Ping.Class, Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (folder.Type))));
                if (0 == (--foldersLeft)) {
                    m_hitMaxFolders = true;
                    break;
                }
            }
            var ping = new XElement (m_ns + Xml.Ping.Ns);
            ping.Add (new XElement (m_ns + Xml.Ping.HeartbeatInterval, BEContext.ProtocolState.HeartbeatInterval.ToString ()));
            ping.Add (xFolders);
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (ping);
            Log.Info (Log.LOG_SYNC, "Sync:\n{0}", doc);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            McProtocolState update;

            Log.Info (Log.LOG_SYNC, "Sync response:\n{0}", doc);

            // NOTE: Important to remember that in this context, SmEvt.E.Success means to do another long-poll.
            string statusString = doc.Root.Element (m_ns + Xml.Ping.Status).Value;
            switch ((Xml.Ping.StatusCode)Convert.ToUInt32 (statusString)) {

            case Xml.Ping.StatusCode.NoChanges_1:
                if (m_hitMaxFolders) {
                    return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "PINGNOCHGMAX");
                }
                return Event.Create ((uint)SmEvt.E.Success, "PINGNOCHG");
            
            case Xml.Ping.StatusCode.Changes_2:
                var folders = doc.Root.Element (m_ns + Xml.Ping.Folders).Elements (m_ns + Xml.Ping.Folder);
                foreach (var xmlFolder in folders) {
                    var folder = BackEnd.Instance.Db.Table<McFolder> ().Single (
                                     rec => BEContext.Account.Id == rec.AccountId && xmlFolder.Value == rec.ServerId);
                    folder.AsSyncRequired = true;
                    folder.Update ();
                }
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "PINGRESYNC");
            
            case Xml.Ping.StatusCode.MissingParams_3:
            case Xml.Ping.StatusCode.SyntaxError_4:
                return Event.Create ((uint)SmEvt.E.HardFail, "PINGHARD0", null, "Xml.Ping.StatusCode.MissingParams/SyntaxError");

            case Xml.Ping.StatusCode.BadHeartbeat_5:
                update = BEContext.ProtocolState;
                update.HeartbeatInterval = uint.Parse (doc.Root.Element (m_ns + Xml.Ping.HeartbeatInterval).Value);
                BEContext.ProtocolState = update;
                return Event.Create ((uint)SmEvt.E.Success, "PINGBADH");

            case Xml.Ping.StatusCode.TooManyFolders_6:
                update = BEContext.ProtocolState;
                update.MaxFolders = uint.Parse (doc.Root.Element (m_ns + Xml.Ping.MaxFolders).Value);
                BEContext.ProtocolState = update;
                return Event.Create ((uint)SmEvt.E.Success, "PINGTMF");

            case Xml.Ping.StatusCode.NeedFolderSync_7:
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "PINGNFS");
            
            case Xml.Ping.StatusCode.ServerError_8:
                return Event.Create ((uint)SmEvt.E.TempFail, "PINGSE", null, "Xml.Ping.StatusCode.ServerError");

            default:
                // FIXME - how do we want to handle unknown status codes?
                Log.Error ("AsPingCommand ProcessResponse UNHANDLED status {0}", statusString);
                return Event.Create ((uint)SmEvt.E.HardFail, "PINGHARD1");
            }
        }
    }
}

