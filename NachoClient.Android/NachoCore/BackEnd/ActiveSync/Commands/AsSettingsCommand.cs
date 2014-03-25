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
        public AsSettingsCommand (IBEContext beContext) : base (Xml.Settings.Ns, Xml.Settings.Ns, beContext)
        {
            // This command does not currently use McPending.
            // TODO: ability to have pending drive OOF setting, etc.
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            // We ask for user information and send device information.
            var settings = new XElement (m_ns + Xml.Settings.Ns, 
                               new XElement (m_ns + Xml.Settings.UserInformation, 
                                   new XElement (m_ns + Xml.Settings.Get)), 
                               DeviceInformation ());
                               
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (settings);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            var xmlSettings = doc.Root;
            var xmlStatus = xmlSettings.Element (m_ns + Xml.Settings.Status);
            var status = (Xml.Settings.StatusCode)uint.Parse (xmlStatus.Value);
            switch (status) {
            case Xml.Settings.StatusCode.Success_1:
                // Check status on DeviceInformation.
                var xmlDeviceInformation = xmlSettings.Element (m_ns + Xml.Settings.DeviceInformation);
                // Tolerate lack of status for DeviceInformation Set.
                if (null != xmlDeviceInformation) {
                    var xmlInnerStatus = xmlDeviceInformation.Element (m_ns + Xml.Settings.Status);
                    var innerStatus = (Xml.Settings.SetGetStatusCode)uint.Parse (xmlInnerStatus.Value);
                    switch (innerStatus) {
                    case Xml.Settings.SetGetStatusCode.Success_1:
                        // break and goto UserInformation.
                        break;
                    case Xml.Settings.SetGetStatusCode.ProtocolError_2:
                    case Xml.Settings.SetGetStatusCode.InvalidArgs_5:
                    case Xml.Settings.SetGetStatusCode.ConflictingArgs_6:
                        BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_SettingsFailed,
                            NcResult.WhyEnum.ProtocolError));
                        return Event.Create ((uint)SmEvt.E.HardFail, "SETTFAIL0A");

                    default:
                        Log.Error ("Unknown inner status code in AsSettingsCommand response: {0}", innerStatus);
                        BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_SettingsFailed,
                            NcResult.WhyEnum.Unknown));
                        return Event.Create ((uint)SmEvt.E.HardFail, "MVUNKSTATUSA");
                    }
                }
                   
                // Capture UserInformation.
                var xmlUserInformation = xmlSettings.Element (m_ns + Xml.Settings.UserInformation);
                if (null != xmlUserInformation) {
                    var xmlInnerStatus = xmlDeviceInformation.Element (m_ns + Xml.Settings.Status);
                    var innerStatus = (Xml.Settings.SetGetStatusCode)uint.Parse (xmlInnerStatus.Value);
                    switch (innerStatus) {
                    case Xml.Settings.SetGetStatusCode.Success_1:
                        // TODO: Capture user information.
                        break;
                    case Xml.Settings.SetGetStatusCode.ProtocolError_2:
                    case Xml.Settings.SetGetStatusCode.InvalidArgs_5:
                    case Xml.Settings.SetGetStatusCode.ConflictingArgs_6:
                        BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_SettingsFailed,
                            NcResult.WhyEnum.ProtocolError));
                        return Event.Create ((uint)SmEvt.E.HardFail, "SETTFAIL0B");

                    default:
                        Log.Error ("Unknown inner status code in AsSettingsCommand response: {0}", innerStatus);
                        BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_SettingsFailed,
                            NcResult.WhyEnum.Unknown));
                        return Event.Create ((uint)SmEvt.E.HardFail, "MVUNKSTATUSB");
                    }
                }
                return Event.Create ((uint)SmEvt.E.Success, "SETTSUCCESS");

            case Xml.Settings.StatusCode.ProtocolError_2:
            case Xml.Settings.StatusCode.InvalidArgs_5:
            case Xml.Settings.StatusCode.ConflictingArgs_6:
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_SettingsFailed,
                    NcResult.WhyEnum.ProtocolError));
                return Event.Create ((uint)SmEvt.E.HardFail, "SETTFAIL0");

            case Xml.Settings.StatusCode.AccessDenied_3:
            case Xml.Settings.StatusCode.PolicyDeny_7:
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_SettingsFailed,
                    NcResult.WhyEnum.AccessDeniedOrBlocked));
                return Event.Create ((uint)SmEvt.E.HardFail, "SETTFAIL1");

            case Xml.Settings.StatusCode.ServerUnavailable_4:
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_SettingsFailed,
                    NcResult.WhyEnum.ServerOffline));
                return Event.Create ((uint)SmEvt.E.TempFail, "SETTFAIL2");

            default:
                Log.Error ("Unknown status code in AsSettingsCommand response: {0}", status);
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_SettingsFailed,
                    NcResult.WhyEnum.Unknown));
                return Event.Create ((uint)SmEvt.E.HardFail, "MVUNKSTATUS");
            }
        }

        public static XElement DeviceInformation ()
        {
            XNamespace Ns = Xml.Settings.Ns;
            return new XElement (Ns + Xml.Settings.DeviceInformation, 
                new XElement (Ns + Xml.Settings.Set,
                    new XElement (Ns + Xml.Settings.Model, Device.Instance.Model ()),
                    new XElement (Ns + Xml.Settings.UserAgent, Device.Instance.UserAgent ()),
                    new XElement (Ns + Xml.Settings.OS, Device.Instance.Os ()),
                    new XElement (Ns + Xml.Settings.OSLanguage, Device.Instance.OsLanguage ()),
                    new XElement (Ns + Xml.Settings.FriendlyName, Device.Instance.FriendlyName ())));
        }
    }
}
