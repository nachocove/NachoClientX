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

        public AsPingCommand (IAsDataSource dataSource) : base (Xml.Ping.Ns, Xml.Ping.Ns, dataSource)
        {
            // Add a 10-second fudge so that orderly timeout doesn't look like a network failure.
            Timeout = new TimeSpan (0, 0, (int)DataSource.ProtocolState.HeartbeatInterval + 10);
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            uint foldersLeft = DataSource.ProtocolState.MaxFolders;
            var xFolders = new XElement (m_ns + Xml.Ping.Folders);
            var folders = DataSource.Owner.Db.Table<McFolder> ().Where (x => x.AccountId == DataSource.Account.Id &&
                          ((uint)Xml.FolderHierarchy.TypeCode.DefaultContacts == x.Type ||
                          (uint)Xml.FolderHierarchy.TypeCode.DefaultCal == x.Type ||
                          (uint)Xml.FolderHierarchy.TypeCode.DefaultInbox == x.Type ||
                          (uint)Xml.FolderHierarchy.TypeCode.DefaultDrafts == x.Type ||
                          (uint)Xml.FolderHierarchy.TypeCode.DefaultSent == x.Type ||
                          (uint)Xml.FolderHierarchy.TypeCode.DefaultOutbox == x.Type ||
                          (uint)Xml.FolderHierarchy.TypeCode.DefaultDeleted == x.Type)
                          );
            foreach (var folder in folders) {
                xFolders.Add (new XElement (m_ns + Xml.Ping.Folder,
                    new XElement (m_ns + Xml.Ping.Id, folder.ServerId),
                    new XElement (m_ns + Xml.Ping.Class, Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (folder.Type))));
                if (0 == (--foldersLeft)) {
                    m_hitMaxFolders = true;
                    break;
                }
            }
            var ping = new XElement (m_ns + Xml.Ping.Ns,
                           new XElement (m_ns + Xml.Ping.HeartbeatInterval,
                               DataSource.ProtocolState.HeartbeatInterval.ToString ()), xFolders);
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (ping);
            Log.Info(Log.LOG_SYNC,"Sync:\n{0}", doc.ToString ());
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            McProtocolState update;

            Log.Info(Log.LOG_SYNC, "Sync response:\n{0}", doc.ToString ());

            // NOTE: Important to remember that in this context, SmEvt.E.Success means to do another long-poll.
            string statusString = doc.Root.Element (m_ns + Xml.Ping.Status).Value;
            switch ((Xml.Ping.StatusCode)Convert.ToUInt32 (statusString)) {

            case Xml.Ping.StatusCode.NoChanges:
                if (m_hitMaxFolders) {
                    return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync);
                }
                return Event.Create ((uint)SmEvt.E.Success);
            
            case Xml.Ping.StatusCode.Changes:
                var folders = doc.Root.Element (m_ns + Xml.Ping.Folders).Elements (m_ns + Xml.Ping.Folder);
                foreach (var xmlFolder in folders) {
                    var folder = DataSource.Owner.Db.Table<McFolder> ().Single (
                                     rec => DataSource.Account.Id == rec.AccountId && xmlFolder.Value == rec.ServerId);
                    folder.AsSyncRequired = true;
                    DataSource.Owner.Db.Update (BackEnd.DbActors.Proto, folder);
                }
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync);
            
            case Xml.Ping.StatusCode.MissingParams:
            case Xml.Ping.StatusCode.SyntaxError:
                return Event.Create ((uint)SmEvt.E.HardFail, null, "Xml.Ping.StatusCode.MissingParams/SyntaxError");

            case Xml.Ping.StatusCode.BadHeartbeat:
                update = DataSource.ProtocolState;
                update.HeartbeatInterval = uint.Parse (doc.Root.Element (m_ns + Xml.Ping.HeartbeatInterval).Value);
                DataSource.ProtocolState = update;
                return Event.Create ((uint)SmEvt.E.Success);

            case Xml.Ping.StatusCode.TooManyFolders:
                update = DataSource.ProtocolState;
                update.MaxFolders = uint.Parse (doc.Root.Element (m_ns + Xml.Ping.MaxFolders).Value);
                DataSource.ProtocolState = update;
                return Event.Create ((uint)SmEvt.E.Success);

            case Xml.Ping.StatusCode.NeedFolderSync:
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync);
            
            case Xml.Ping.StatusCode.ServerError:
                return Event.Create ((uint)SmEvt.E.TempFail, null, "Xml.Ping.StatusCode.ServerError");

            default:
                // FIXME - how do we want to handle unknown status codes?
                Log.Error ("AsPingCommand ProcessResponse UNHANDLED status {0}", statusString);
                return Event.Create ((uint)SmEvt.E.HardFail);
            }
        }
    }
}

