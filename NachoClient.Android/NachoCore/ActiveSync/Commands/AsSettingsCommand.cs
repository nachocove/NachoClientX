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
        public AsSettingsCommand (IAsDataSource dataSource) : base (Xml.Settings.Ns, Xml.Settings.Ns, dataSource)
        {
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var settings = new XElement (m_ns + Xml.Settings.Ns, 
                               new XElement (m_ns + Xml.Settings.UserInformation, 
                                   new XElement (m_ns + Xml.Settings.Get)), DeviceInformation ());
                               
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (settings);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            // FIXME - evaluate response.
            return Event.Create ((uint)SmEvt.E.Success);
        }

        public static XElement DeviceInformation ()
        {
            XNamespace settingsNs = Xml.Settings.Ns;
            return new XElement (settingsNs + Xml.Settings.DeviceInformation, 
                new XElement (settingsNs + Xml.Settings.Set,
                    new XElement (settingsNs + Xml.Settings.Model, Device.Instance.Model ()),
                    new XElement (settingsNs + Xml.Settings.UserAgent, Device.Instance.UserAgent ()),
                    new XElement (settingsNs + Xml.Settings.OS, Device.Instance.Os ()),
                    new XElement (settingsNs + Xml.Settings.OSLanguage, Device.Instance.OsLanguage ()),
                    new XElement (settingsNs + Xml.Settings.FriendlyName, Device.Instance.FriendlyName ())));
        }
    }
}
