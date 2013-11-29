using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using System.IO;

namespace NachoCore.ActiveSync
{
    public partial class AsSyncCommand : AsCommand
    {
        // public for unit tests
        public void AddEvent (XElement command, NcFolder folder)
        {
            // <ApplicationData>...</ApplicationData>
            Log.Info (Log.LOG_CALENDAR, "AddEvent\n{0}", command.ToString ());
            var appData = command.Element (m_ns + Xml.AirSync.ApplicationData);
            foreach (var child in appData.Elements()) {
                Console.WriteLine ("addEvent: " + child.Name.LocalName + " value=" + child.Value);
            }
        }
        // public for unit tests
        public void UpdateEvent (XElement command, NcFolder folder)
        {
            // <ApplicationData>...</ApplicationData>
            var appData = command.Element (m_ns + Xml.AirSync.ApplicationData);
            Log.Info (Log.LOG_CALENDAR, "UpdateEvent\n{0}", appData.ToString ());
            foreach (var child in appData.Elements()) {
                Console.WriteLine ("updateEvent: " + child.Name.LocalName + " value=" + child.Value);
            }
        }


    }
}
