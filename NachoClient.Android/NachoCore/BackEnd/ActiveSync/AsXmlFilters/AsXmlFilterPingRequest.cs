using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterPingRequest : NcXmlFilter
    {
        public AsXmlFilterPingRequest () : base ("Ping")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;

            // Ping
            node0 = new NcXmlFilterNode ("Ping", RedactionType.NONE, RedactionType.NONE);
            // HeartbeatInterval
            node1 = new NcXmlFilterNode ("HeartbeatInterval", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Ping -> HeartbeatInterval
            // Folders
            node1 = new NcXmlFilterNode ("Folders", RedactionType.NONE, RedactionType.NONE);
            // Folder
            node2 = new NcXmlFilterNode ("Folder", RedactionType.NONE, RedactionType.NONE);
            // Id
            node3 = new NcXmlFilterNode ("Id", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Folder -> Id
            // Class
            node3 = new NcXmlFilterNode ("Class", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Folder -> Class
            node1.Add(node2); // Folders -> Folder
            node0.Add(node1); // Ping -> Folders
            
            Root = node0;
        }
    }
}
