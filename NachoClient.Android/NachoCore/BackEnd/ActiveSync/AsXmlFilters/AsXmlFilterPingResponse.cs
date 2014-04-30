using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterPingResponse : NcXmlFilter
    {
        public AsXmlFilterPingResponse () : base ("Ping")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;

            // Ping
            node0 = new NcXmlFilterNode ("Ping", RedactionType.NONE, RedactionType.NONE);
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Ping -> Status
            // Folders
            node1 = new NcXmlFilterNode ("Folders", RedactionType.NONE, RedactionType.NONE);
            // Folder
            node2 = new NcXmlFilterNode ("Folder", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Folders -> Folder
            node0.Add(node1); // Ping -> Folders
            // MaxFolders
            node1 = new NcXmlFilterNode ("MaxFolders", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Ping -> MaxFolders
            // HeartbeatInterval
            node1 = new NcXmlFilterNode ("HeartbeatInterval", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Ping -> HeartbeatInterval
            
            Root = node0;
        }
    }
}
