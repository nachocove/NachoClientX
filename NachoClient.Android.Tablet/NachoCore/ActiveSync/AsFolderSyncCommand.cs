using System;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public class AsFolderSyncCommand : AsCommand
	{
		private enum StatusFolderSync : uint {Success=1, Retry=6, ReSync=9, BadFormat=10, 
			Unknown=11, ServerFail=12}; // FIXME: with Unknown, we need to retry & watch for a loop.

		public AsFolderSyncCommand (IAsDataSource dataSource) : base("FolderSync", dataSource) {}

		protected override XDocument ToXDocument () {
			XNamespace ns = "FolderHierarchy";
			var folderSync = new XElement (ns + "FolderSync", 
			                               new XElement (ns + "SyncKey", m_dataSource.ProtocolState.AsSyncKey));
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (folderSync);
			return doc;
		}

		protected override uint ProcessResponse (HttpResponseMessage response, XDocument doc)
		{
			XNamespace ns = "FolderHierarchy";
			switch ((StatusFolderSync)Convert.ToUInt32 (doc.Root.Element (ns+"Status").Value)) {
			case StatusFolderSync.Success:
				m_dataSource.ProtocolState.AsSyncKey = doc.Root.Element (ns + "SyncKey").Value;
				// FIXME - update DB w/folders.
				return (uint)Ev.Success;
			default:
				return (uint)Ev.Failure;
			}
		}
	}
}

