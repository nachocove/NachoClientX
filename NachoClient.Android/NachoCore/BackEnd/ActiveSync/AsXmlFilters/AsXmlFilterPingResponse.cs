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
            NcXmlFilterNode node3 = null;

            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Ping
            node1 = new NcXmlFilterNode ("Ping", RedactionType.NONE, RedactionType.NONE);
            // Folders
            node2 = new NcXmlFilterNode ("Folders", RedactionType.NONE, RedactionType.NONE);
            // Folder
            node3 = new NcXmlFilterNode ("Folder", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Folders -> Folder
            node1.Add(node2); // Ping -> Folders
            // HeartbeatInterval
            node2 = new NcXmlFilterNode ("HeartbeatInterval", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Ping -> HeartbeatInterval
            // MaxFolders
            node2 = new NcXmlFilterNode ("MaxFolders", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Ping -> MaxFolders
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Ping -> Status
            node0.Add(node1);
            
            Root = node0;
        }
    }
}
