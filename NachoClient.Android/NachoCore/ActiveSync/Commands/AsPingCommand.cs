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

		public AsPingCommand (IAsDataSource dataSource) : base(Xml.Ping.Ns, Xml.Ping.Ns, dataSource) {
			// Add a 10-second fudge so that orderly timeout doesn't look like a network failure.
			Timeout = new TimeSpan (0, 0, (int)DataSource.ProtocolState.HeartbeatInterval + 10);
		}

        public override XDocument ToXDocument (AsHttpOperation Sender) {
			uint foldersLeft = DataSource.ProtocolState.MaxFolders;
			var xFolders = new XElement (m_ns + Xml.Ping.Folders);
			var folders = DataSource.Owner.Db.Table<NcFolder> ().Where (x => x.AccountId == DataSource.Account.Id && "Mail:DEFAULT" == x.ServerId);
			foreach (var folder in folders) {
				xFolders.Add (new XElement (m_ns + Xml.Ping.Folder,
				                           new XElement (m_ns + Xml.Ping.Id, folder.ServerId),
				                           new XElement (m_ns + Xml.Ping.Class, "Email")));
				if (0 == (-- foldersLeft)) {
					m_hitMaxFolders = true;
					break;
				}
			}
			var ping = new XElement (m_ns + Xml.Ping.Ns,
			                         new XElement (m_ns + Xml.Ping.HeartbeatInterval,
			              						   DataSource.ProtocolState.HeartbeatInterval.ToString ()), xFolders);
			var doc = AsCommand.ToEmptyXDocument ();
			doc.Add (ping);
			return doc;
		}
        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc) {
			// NOTE: Important to remember that in this context, Ev.Success means to do another long-poll.
			switch ((Xml.Ping.StatusCode)Convert.ToUInt32 (doc.Root.Element (m_ns + Xml.Ping.Status).Value)) {

            case Xml.Ping.StatusCode.NoChanges:
                if (m_hitMaxFolders) {
                    return Event.Create ((uint)AsProtoControl.Lev.ReSync);
                }
                return Event.Create ((uint)Ev.Success);
			
            case Xml.Ping.StatusCode.Changes:
                var folders = doc.Root.Element (m_ns + Xml.Ping.Folders).Elements (m_ns + Xml.Ping.Folder);
                foreach (var xmlFolder in folders) {// FIXME - accountid.
                    var folder = DataSource.Owner.Db.Table<NcFolder> ().Single (rec => xmlFolder.Value == rec.ServerId);
                    folder.AsSyncRequired = true;
                    DataSource.Owner.Db.Update (BackEnd.DbActors.Proto, folder);
                }
                return Event.Create ((uint)AsProtoControl.Lev.ReSync);
			
            case Xml.Ping.StatusCode.MissingParams:
            case Xml.Ping.StatusCode.SyntaxError:
                return Event.Create ((uint)Ev.HardFail);

            case Xml.Ping.StatusCode.BadHeartbeat:
                DataSource.ProtocolState.HeartbeatInterval = 
					uint.Parse (doc.Root.Element (m_ns + Xml.Ping.HeartbeatInterval).Value);
                return Event.Create ((uint)Ev.Success);

            case Xml.Ping.StatusCode.TooManyFolders:
                DataSource.ProtocolState.MaxFolders =
					uint.Parse (doc.Root.Element (m_ns + Xml.Ping.MaxFolders).Value);
                return Event.Create ((uint)Ev.Success);

            case Xml.Ping.StatusCode.NeedFolderSync:
                return Event.Create ((uint)AsProtoControl.Lev.ReFSync);
			
            case Xml.Ping.StatusCode.ServerError:
                return Event.Create ((uint)Ev.TempFail);

            default:
				// FIXME - how do we want to handle unknown status codes?
                return Event.Create ((uint)Ev.HardFail);
			}
		}
	}
}

