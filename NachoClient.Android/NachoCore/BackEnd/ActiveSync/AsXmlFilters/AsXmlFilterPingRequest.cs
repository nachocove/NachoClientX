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
            NcXmlFilterNode node4 = null;

            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Ping
            node1 = new NcXmlFilterNode ("Ping", RedactionType.NONE, RedactionType.NONE);
            // Folders
            node2 = new NcXmlFilterNode ("Folders", RedactionType.NONE, RedactionType.NONE);
            // Folder
            node3 = new NcXmlFilterNode ("Folder", RedactionType.NONE, RedactionType.NONE);
            // Class
            node4 = new NcXmlFilterNode ("Class", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Folder -> Class
            // Id
            node4 = new NcXmlFilterNode ("Id", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Folder -> Id
            node2.Add(node3); // Folders -> Folder
            node1.Add(node2); // Ping -> Folders
            // HeartbeatInterval
            node2 = new NcXmlFilterNode ("HeartbeatInterval", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Ping -> HeartbeatInterval
            node0.Add(node1);
            
            Root = node0;
        }
    }
}
