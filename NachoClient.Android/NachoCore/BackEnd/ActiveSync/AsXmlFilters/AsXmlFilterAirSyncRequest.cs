using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterAirSyncRequest : NcXmlFilter
    {
        public AsXmlFilterAirSyncRequest () : base ("AirSync")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;
            NcXmlFilterNode node5 = null;
            NcXmlFilterNode node6 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // SyncKey
            node1 = new NcXmlFilterNode ("SyncKey", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> SyncKey
            // ClientId
            node1 = new NcXmlFilterNode ("ClientId", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> ClientId
            // ServerId
            node1 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> ServerId
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Status
            // Class
            node1 = new NcXmlFilterNode ("Class", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Class
            // CollectionId
            node1 = new NcXmlFilterNode ("CollectionId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node0.Add(node1); // xml -> CollectionId
            // GetChanges
            node1 = new NcXmlFilterNode ("GetChanges", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> GetChanges
            // MoreAvailable
            node1 = new NcXmlFilterNode ("MoreAvailable", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> MoreAvailable
            // WindowSize
            node1 = new NcXmlFilterNode ("WindowSize", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> WindowSize
            // FilterType
            node1 = new NcXmlFilterNode ("FilterType", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> FilterType
            // Conflict
            node1 = new NcXmlFilterNode ("Conflict", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Conflict
            // DeletesAsMoves
            node1 = new NcXmlFilterNode ("DeletesAsMoves", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> DeletesAsMoves
            // Supported
            node1 = new NcXmlFilterNode ("Supported", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Supported
            // SoftDelete
            node1 = new NcXmlFilterNode ("SoftDelete", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node2 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // SoftDelete -> ServerId
            node0.Add(node1); // xml -> SoftDelete
            // MIMESupport
            node1 = new NcXmlFilterNode ("MIMESupport", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> MIMESupport
            // MIMETruncation
            node1 = new NcXmlFilterNode ("MIMETruncation", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> MIMETruncation
            // Wait
            node1 = new NcXmlFilterNode ("Wait", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Wait
            // Limit
            node1 = new NcXmlFilterNode ("Limit", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Limit
            // Partial
            node1 = new NcXmlFilterNode ("Partial", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Partial
            // ConversationMode
            node1 = new NcXmlFilterNode ("ConversationMode", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> ConversationMode
            // MaxItems
            node1 = new NcXmlFilterNode ("MaxItems", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> MaxItems
            // HeartbeatInterval
            node1 = new NcXmlFilterNode ("HeartbeatInterval", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> HeartbeatInterval
            // Options
            node1 = new NcXmlFilterNode ("Options", RedactionType.NONE, RedactionType.NONE);
            // FilterType
            node2 = new NcXmlFilterNode ("FilterType", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Options -> FilterType
            // Class
            node2 = new NcXmlFilterNode ("Class", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Options -> Class
            // Conflict
            node2 = new NcXmlFilterNode ("Conflict", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Options -> Conflict
            // MIMESupport
            node2 = new NcXmlFilterNode ("MIMESupport", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Options -> MIMESupport
            // MIMETruncation
            node2 = new NcXmlFilterNode ("MIMETruncation", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Options -> MIMETruncation
            // MaxItems
            node2 = new NcXmlFilterNode ("MaxItems", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Options -> MaxItems
            node0.Add(node1); // xml -> Options
            // Sync
            node1 = new NcXmlFilterNode ("Sync", RedactionType.NONE, RedactionType.NONE);
            // Collections
            node2 = new NcXmlFilterNode ("Collections", RedactionType.NONE, RedactionType.NONE);
            // Collection
            node3 = new NcXmlFilterNode ("Collection", RedactionType.NONE, RedactionType.NONE);
            // SyncKey
            node4 = new NcXmlFilterNode ("SyncKey", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Collection -> SyncKey
            // CollectionId
            node4 = new NcXmlFilterNode ("CollectionId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node3.Add(node4); // Collection -> CollectionId
            // Supported
            node4 = new NcXmlFilterNode ("Supported", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Collection -> Supported
            // DeletesAsMoves
            node4 = new NcXmlFilterNode ("DeletesAsMoves", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Collection -> DeletesAsMoves
            // GetChanges
            node4 = new NcXmlFilterNode ("GetChanges", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Collection -> GetChanges
            // WindowSize
            node4 = new NcXmlFilterNode ("WindowSize", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Collection -> WindowSize
            // ConversationMode
            node4 = new NcXmlFilterNode ("ConversationMode", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Collection -> ConversationMode
            // Options
            node4 = new NcXmlFilterNode ("Options", RedactionType.NONE, RedactionType.NONE);
            // FilterType
            node5 = new NcXmlFilterNode ("FilterType", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Options -> FilterType
            // Class
            node5 = new NcXmlFilterNode ("Class", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Options -> Class
            // Conflict
            node5 = new NcXmlFilterNode ("Conflict", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Options -> Conflict
            // MIMESupport
            node5 = new NcXmlFilterNode ("MIMESupport", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Options -> MIMESupport
            // MIMETruncation
            node5 = new NcXmlFilterNode ("MIMETruncation", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Options -> MIMETruncation
            // MaxItems
            node5 = new NcXmlFilterNode ("MaxItems", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Options -> MaxItems
            node3.Add(node4); // Collection -> Options
            // Commands
            node4 = new NcXmlFilterNode ("Commands", RedactionType.NONE, RedactionType.NONE);
            // Change
            node5 = new NcXmlFilterNode ("Change", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node6 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // Change -> ServerId
            // ApplicationData
            node6 = new NcXmlFilterNode ("ApplicationData", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // Change -> ApplicationData
            node4.Add(node5); // Commands -> Change
            // Delete
            node5 = new NcXmlFilterNode ("Delete", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node6 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // Delete -> ServerId
            node4.Add(node5); // Commands -> Delete
            // Add
            node5 = new NcXmlFilterNode ("Add", RedactionType.NONE, RedactionType.NONE);
            // Class
            node6 = new NcXmlFilterNode ("Class", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // Add -> Class
            // ClientId
            node6 = new NcXmlFilterNode ("ClientId", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // Add -> ClientId
            // ApplicationData
            node6 = new NcXmlFilterNode ("ApplicationData", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // Add -> ApplicationData
            node4.Add(node5); // Commands -> Add
            // Fetch
            node5 = new NcXmlFilterNode ("Fetch", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node6 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // Fetch -> ServerId
            node4.Add(node5); // Commands -> Fetch
            node3.Add(node4); // Collection -> Commands
            node2.Add(node3); // Collections -> Collection
            node1.Add(node2); // Sync -> Collections
            // Wait
            node2 = new NcXmlFilterNode ("Wait", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Sync -> Wait
            // HeartbeatInterval
            node2 = new NcXmlFilterNode ("HeartbeatInterval", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Sync -> HeartbeatInterval
            // WindowSize
            node2 = new NcXmlFilterNode ("WindowSize", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Sync -> WindowSize
            // Partial
            node2 = new NcXmlFilterNode ("Partial", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Sync -> Partial
            node0.Add(node1); // xml -> Sync
            
            Root = node0;
        }
    }
}
