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

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Ping
            node1 = new NcXmlFilterNode ("Ping", RedactionType.NONE, RedactionType.NONE);
            // HeartbeatInterval
            node2 = new NcXmlFilterNode ("HeartbeatInterval", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Ping -> HeartbeatInterval
            // Folders
            node2 = new NcXmlFilterNode ("Folders", RedactionType.NONE, RedactionType.NONE);
            // Folder
            node3 = new NcXmlFilterNode ("Folder", RedactionType.NONE, RedactionType.NONE);
            // Id
            node4 = new NcXmlFilterNode ("Id", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Folder -> Id
            // Class
            node4 = new NcXmlFilterNode ("Class", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Folder -> Class
            node2.Add(node3); // Folders -> Folder
            node1.Add(node2); // Ping -> Folders
            node0.Add(node1); // xml -> Ping
            
            Root = node0;
        }
    }
}
