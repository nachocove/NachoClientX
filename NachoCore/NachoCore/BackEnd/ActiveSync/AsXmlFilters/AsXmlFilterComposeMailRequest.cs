using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterComposeMailRequest : NcXmlFilter
    {
        public AsXmlFilterComposeMailRequest () : base ("ComposeMail")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // ClientId
            node1 = new NcXmlFilterNode ("ClientId", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> ClientId
            // AccountId
            node1 = new NcXmlFilterNode ("AccountId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> AccountId
            // SaveInSentItems
            node1 = new NcXmlFilterNode ("SaveInSentItems", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> SaveInSentItems
            // ReplaceMime
            node1 = new NcXmlFilterNode ("ReplaceMime", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> ReplaceMime
            // Mime
            node1 = new NcXmlFilterNode ("Mime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Mime
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Status
            // Source
            node1 = new NcXmlFilterNode ("Source", RedactionType.NONE, RedactionType.NONE);
            // FolderId
            node2 = new NcXmlFilterNode ("FolderId", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Source -> FolderId
            // ItemId
            node2 = new NcXmlFilterNode ("ItemId", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Source -> ItemId
            // LongId
            node2 = new NcXmlFilterNode ("LongId", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Source -> LongId
            // InstanceId
            node2 = new NcXmlFilterNode ("InstanceId", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Source -> InstanceId
            node0.Add(node1); // xml -> Source
            // SendMail
            node1 = new NcXmlFilterNode ("SendMail", RedactionType.NONE, RedactionType.NONE);
            // ClientId
            node2 = new NcXmlFilterNode ("ClientId", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // SendMail -> ClientId
            // AccountId
            node2 = new NcXmlFilterNode ("AccountId", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // SendMail -> AccountId
            // SaveInSentItems
            node2 = new NcXmlFilterNode ("SaveInSentItems", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // SendMail -> SaveInSentItems
            // Mime
            node2 = new NcXmlFilterNode ("Mime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // SendMail -> Mime
            node0.Add(node1); // xml -> SendMail
            // SmartForward
            node1 = new NcXmlFilterNode ("SmartForward", RedactionType.NONE, RedactionType.NONE);
            // ClientId
            node2 = new NcXmlFilterNode ("ClientId", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // SmartForward -> ClientId
            // Source
            node2 = new NcXmlFilterNode ("Source", RedactionType.NONE, RedactionType.NONE);
            // FolderId
            node3 = new NcXmlFilterNode ("FolderId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Source -> FolderId
            // ItemId
            node3 = new NcXmlFilterNode ("ItemId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Source -> ItemId
            // LongId
            node3 = new NcXmlFilterNode ("LongId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Source -> LongId
            // InstanceId
            node3 = new NcXmlFilterNode ("InstanceId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Source -> InstanceId
            node1.Add(node2); // SmartForward -> Source
            // AccountId
            node2 = new NcXmlFilterNode ("AccountId", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // SmartForward -> AccountId
            // SaveInSentItems
            node2 = new NcXmlFilterNode ("SaveInSentItems", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // SmartForward -> SaveInSentItems
            // ReplaceMime
            node2 = new NcXmlFilterNode ("ReplaceMime", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // SmartForward -> ReplaceMime
            // Mime
            node2 = new NcXmlFilterNode ("Mime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // SmartForward -> Mime
            node0.Add(node1); // xml -> SmartForward
            // SmartReply
            node1 = new NcXmlFilterNode ("SmartReply", RedactionType.NONE, RedactionType.NONE);
            // ClientId
            node2 = new NcXmlFilterNode ("ClientId", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // SmartReply -> ClientId
            // Source
            node2 = new NcXmlFilterNode ("Source", RedactionType.NONE, RedactionType.NONE);
            // FolderId
            node3 = new NcXmlFilterNode ("FolderId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Source -> FolderId
            // ItemId
            node3 = new NcXmlFilterNode ("ItemId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Source -> ItemId
            // LongId
            node3 = new NcXmlFilterNode ("LongId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Source -> LongId
            // InstanceId
            node3 = new NcXmlFilterNode ("InstanceId", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Source -> InstanceId
            node1.Add(node2); // SmartReply -> Source
            // AccountId
            node2 = new NcXmlFilterNode ("AccountId", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // SmartReply -> AccountId
            // SaveInSentItems
            node2 = new NcXmlFilterNode ("SaveInSentItems", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // SmartReply -> SaveInSentItems
            // ReplaceMime
            node2 = new NcXmlFilterNode ("ReplaceMime", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // SmartReply -> ReplaceMime
            // Mime
            node2 = new NcXmlFilterNode ("Mime", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // SmartReply -> Mime
            node0.Add(node1); // xml -> SmartReply
            
            Root = node0;
        }
    }
}
