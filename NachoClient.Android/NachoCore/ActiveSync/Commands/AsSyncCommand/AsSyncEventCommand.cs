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
        private void AddEvent (XElement command, NcFolder folder)
        {
            // <ApplicationData>...</ApplicationData>
            var appData = command.Element (m_ns + Xml.AirSync.ApplicationData);
            foreach (var child in appData.Elements()) {
                Console.WriteLine ("addEvent: " + child.Name.LocalName + " value=" + child.Value);
            }
        }
    }
    // <Body xmlns="AirSyncBase:"> <Type> 1 </Type> <Data> </Data> </Body>
    // <DTStamp xmlns="Calendar:"> 20131123T190243Z </DTStamp>
    // <StartTime xmlns="Calendar:"> 20131123T223000Z </StartTime>
    // <EndTime xmlns="Calendar:"> 20131123T233000Z </EndTime>
    // <Location xmlns="Calendar:"> the Dogg House!  </Location>
    // <Subject xmlns="Calendar:"> Big dog party at the Dogg House!  </Subject>
    // <UID xmlns="Calendar:"> 3rrr5stn6eld9qmv8dviolj3u0@google.com </UID>
    // <Sensitivity xmlns="Calendar:"> 0 </Sensitivity>
    // <BusyStatus xmlns="Calendar:"> 2 </BusyStatus>
    // <AllDayEvent xmlns="Calendar:"> 0 </AllDayEvent>
    // <Reminder xmlns="Calendar:"> 10 </Reminder>
    // <MeetingStatus xmlns="Calendar:"> 0 </MeetingStatus>
    // <TimeZone xmlns="Calendar:"> LAEAAEUAUw...P///w== </TimeZone>
    // <Organizer_Email xmlns="Calendar:"> steves@nachocove.com </Organizer_Email>
    // <Organizer_Name xmlns="Calendar:"> Steve Scalpone </Organizer_Name>
}
