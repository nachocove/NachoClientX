using System;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public class AsSettingsCommand : AsCommand
	{
		private enum StatusSettings : uint {Success=1, ProtocolError=2, AccessDenied=3, ServerUnavailable=4,
			InvalidArgs=5, ConflictingArgs=6, PolicyDeny=7};

		public AsSettingsCommand (IAsDataSource dataSource) : base (Xml.Settings.Ns, Xml.Settings.Ns, dataSource) {}

		protected override XDocument ToXDocument () {
			var settings = new XElement (m_ns + Xml.Settings.Ns, 
			                             new XElement (m_ns + Xml.Settings.UserInformation, 
			                             	new XElement (m_ns + Xml.Settings.Get)), 
			                             new XElement (m_ns + Xml.Settings.DeviceInformation, 
			                              	new XElement (m_ns + Xml.Settings.Set,
			              						new XElement (m_ns + Xml.Settings.Model, NcDevice.Model()),
			              						new XElement (m_ns + Xml.Settings.OS, NcDevice.Os()),
			              						new XElement (m_ns + Xml.Settings.OSLanguage, NcDevice.OsLanguage()),
			              						new XElement (m_ns + Xml.Settings.FriendlyName, NcDevice.FriendlyName()))));
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (settings);
			return doc;
		}
		protected override uint ProcessResponse (HttpResponseMessage response, XDocument doc) {
			return (uint)Ev.Success;
		}
	}
}
