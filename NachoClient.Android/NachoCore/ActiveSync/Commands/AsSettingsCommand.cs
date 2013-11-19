using System;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.ActiveSync
{
    public class AsSettingsCommand : AsCommand
    {
        private enum StatusSettings : uint {Success=1, ProtocolError=2, AccessDenied=3, ServerUnavailable=4,
            InvalidArgs=5, ConflictingArgs=6, PolicyDeny=7};

        public AsSettingsCommand (IAsDataSource dataSource) : base (Xml.Settings.Ns, Xml.Settings.Ns, dataSource) {}

        public override XDocument ToXDocument (AsHttpOperation Sender) {
            var settings = new XElement (m_ns + Xml.Settings.Ns, 
                                         new XElement (m_ns + Xml.Settings.UserInformation, 
                                            new XElement (m_ns + Xml.Settings.Get)), 
                                         new XElement (m_ns + Xml.Settings.DeviceInformation, 
                                            new XElement (m_ns + Xml.Settings.Set,
                                                new XElement (m_ns + Xml.Settings.Model, Device.Instance.Model()),
                                                new XElement (m_ns + Xml.Settings.OS, Device.Instance.Os()),
                                                new XElement (m_ns + Xml.Settings.OSLanguage, Device.Instance.OsLanguage()),
                                                new XElement (m_ns + Xml.Settings.FriendlyName, Device.Instance.FriendlyName()))));
            var doc = AsCommand.ToEmptyXDocument();
            doc.Add (settings);
            return doc;
        }
        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc) {
            return Event.Create ((uint)SmEvt.E.Success);
        }
    }
}
