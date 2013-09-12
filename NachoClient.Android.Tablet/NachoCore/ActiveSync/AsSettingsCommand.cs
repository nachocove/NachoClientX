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

		public AsSettingsCommand (IAsDataSource dataSource) : base("Settings", dataSource) {}

		protected override XDocument ToXDocument () {
			XNamespace ns = "Settings";
			var settings = new XElement (ns + "Settings", 
			                             new XElement (ns + "UserInformation", new XElement (ns + "Get")),
			                             new XElement (ns + "DeviceInformation", 
			                                           new XElement (ns + "Set",
			                            							new XElement (ns + "Model", NcDevice.Model()),
			                            							new XElement (ns + "OS", NcDevice.Os()),
			                            							new XElement (ns + "OSLanguage", NcDevice.OsLanguage()),
			                            							new XElement (ns + "FriendlyName", NcDevice.FriendlyName()))));
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (settings);
			return doc;
		}
		protected override uint ProcessResponse (HttpResponseMessage response, XDocument doc) {
			return (uint)Ev.Success;
		}
	}
}
