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

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Ping
            node1 = new NcXmlFilterNode ("Ping", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Ping -> Status
            // Folders
            node2 = new NcXmlFilterNode ("Folders", RedactionType.NONE, RedactionType.NONE);
            // Folder
            node3 = new NcXmlFilterNode ("Folder", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Folders -> Folder
            node1.Add(node2); // Ping -> Folders
            // MaxFolders
            node2 = new NcXmlFilterNode ("MaxFolders", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Ping -> MaxFolders
            // HeartbeatInterval
            node2 = new NcXmlFilterNode ("HeartbeatInterval", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Ping -> HeartbeatInterval
            node0.Add(node1); // xml -> Ping
            
            Root = node0;
        }
    }
}
