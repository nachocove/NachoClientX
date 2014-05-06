using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterSyncRequest : NcXmlFilter
    {
        public AsXmlFilterSyncRequest () : base ("AirSync")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;
            NcXmlFilterNode node5 = null;

            // SyncKey
            node0 = new NcXmlFilterNode ("SyncKey", RedactionType.FULL, RedactionType.FULL);
            // ClientId
            node0 = new NcXmlFilterNode ("ClientId", RedactionType.FULL, RedactionType.FULL);
            // ServerId
            node0 = new NcXmlFilterNode ("ServerId", RedactionType.FULL, RedactionType.FULL);
            // Status
            node0 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            // Class
            node0 = new NcXmlFilterNode ("Class", RedactionType.FULL, RedactionType.FULL);
            // CollectionId
            node0 = new NcXmlFilterNode ("CollectionId", RedactionType.FULL, RedactionType.FULL);
            // GetChanges
            node0 = new NcXmlFilterNode ("GetChanges", RedactionType.FULL, RedactionType.FULL);
            // MoreAvailable
            node0 = new NcXmlFilterNode ("MoreAvailable", RedactionType.FULL, RedactionType.FULL);
            // WindowSize
            node0 = new NcXmlFilterNode ("WindowSize", RedactionType.FULL, RedactionType.FULL);
            // FilterType
            node0 = new NcXmlFilterNode ("FilterType", RedactionType.FULL, RedactionType.FULL);
            // Conflict
            node0 = new NcXmlFilterNode ("Conflict", RedactionType.FULL, RedactionType.FULL);
            // DeletesAsMoves
            node0 = new NcXmlFilterNode ("DeletesAsMoves", RedactionType.FULL, RedactionType.FULL);
            // Supported
            node0 = new NcXmlFilterNode ("Supported", RedactionType.FULL, RedactionType.FULL);
            // SoftDelete
            node0 = new NcXmlFilterNode ("SoftDelete", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node1 = new NcXmlFilterNode ("ServerId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // SoftDelete -> ServerId
            // MIMESupport
            node0 = new NcXmlFilterNode ("MIMESupport", RedactionType.FULL, RedactionType.FULL);
            // MIMETruncation
            node0 = new NcXmlFilterNode ("MIMETruncation", RedactionType.FULL, RedactionType.FULL);
            // Wait
            node0 = new NcXmlFilterNode ("Wait", RedactionType.FULL, RedactionType.FULL);
            // Limit
            node0 = new NcXmlFilterNode ("Limit", RedactionType.FULL, RedactionType.FULL);
            // Partial
            node0 = new NcXmlFilterNode ("Partial", RedactionType.FULL, RedactionType.FULL);
            // ConversationMode
            node0 = new NcXmlFilterNode ("ConversationMode", RedactionType.FULL, RedactionType.FULL);
            // MaxItems
            node0 = new NcXmlFilterNode ("MaxItems", RedactionType.FULL, RedactionType.FULL);
            // HeartbeatInterval
            node0 = new NcXmlFilterNode ("HeartbeatInterval", RedactionType.FULL, RedactionType.FULL);
            // Options
            node0 = new NcXmlFilterNode ("Options", RedactionType.NONE, RedactionType.NONE);
            // FilterType
            node1 = new NcXmlFilterNode ("FilterType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Options -> FilterType
            // Class
            node1 = new NcXmlFilterNode ("Class", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Options -> Class
            // Conflict
            node1 = new NcXmlFilterNode ("Conflict", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Options -> Conflict
            // MIMESupport
            node1 = new NcXmlFilterNode ("MIMESupport", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Options -> MIMESupport
            // MIMETruncation
            node1 = new NcXmlFilterNode ("MIMETruncation", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Options -> MIMETruncation
            // MaxItems
            node1 = new NcXmlFilterNode ("MaxItems", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Options -> MaxItems
            // Sync
            node0 = new NcXmlFilterNode ("Sync", RedactionType.NONE, RedactionType.NONE);
            // Collections
            node1 = new NcXmlFilterNode ("Collections", RedactionType.NONE, RedactionType.NONE);
            // Collection
            node2 = new NcXmlFilterNode ("Collection", RedactionType.NONE, RedactionType.NONE);
            // SyncKey
            node3 = new NcXmlFilterNode ("SyncKey", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Collection -> SyncKey
            // CollectionId
            node3 = new NcXmlFilterNode ("CollectionId", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Collection -> CollectionId
            // Supported
            node3 = new NcXmlFilterNode ("Supported", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Collection -> Supported
            // DeletesAsMoves
            node3 = new NcXmlFilterNode ("DeletesAsMoves", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Collection -> DeletesAsMoves
            // GetChanges
            node3 = new NcXmlFilterNode ("GetChanges", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Collection -> GetChanges
            // WindowSize
            node3 = new NcXmlFilterNode ("WindowSize", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Collection -> WindowSize
            // ConversationMode
            node3 = new NcXmlFilterNode ("ConversationMode", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Collection -> ConversationMode
            // Options
            node3 = new NcXmlFilterNode ("Options", RedactionType.NONE, RedactionType.NONE);
            // FilterType
            node4 = new NcXmlFilterNode ("FilterType", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> FilterType
            // Class
            node4 = new NcXmlFilterNode ("Class", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> Class
            // Conflict
            node4 = new NcXmlFilterNode ("Conflict", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> Conflict
            // MIMESupport
            node4 = new NcXmlFilterNode ("MIMESupport", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> MIMESupport
            // MIMETruncation
            node4 = new NcXmlFilterNode ("MIMETruncation", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> MIMETruncation
            // MaxItems
            node4 = new NcXmlFilterNode ("MaxItems", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Options -> MaxItems
            node2.Add(node3); // Collection -> Options
            // Commands
            node3 = new NcXmlFilterNode ("Commands", RedactionType.NONE, RedactionType.NONE);
            // Change
            node4 = new NcXmlFilterNode ("Change", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node5 = new NcXmlFilterNode ("ServerId", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Change -> ServerId
            // ApplicationData
            node5 = new NcXmlFilterNode ("ApplicationData", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Change -> ApplicationData
            node3.Add(node4); // Commands -> Change
            // Delete
            node4 = new NcXmlFilterNode ("Delete", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node5 = new NcXmlFilterNode ("ServerId", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Delete -> ServerId
            node3.Add(node4); // Commands -> Delete
            // Add
            node4 = new NcXmlFilterNode ("Add", RedactionType.NONE, RedactionType.NONE);
            // Class
            node5 = new NcXmlFilterNode ("Class", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Add -> Class
            // ClientId
            node5 = new NcXmlFilterNode ("ClientId", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Add -> ClientId
            // ApplicationData
            node5 = new NcXmlFilterNode ("ApplicationData", RedactionType.NONE, RedactionType.NONE);
            node4.Add(node5); // Add -> ApplicationData
            node3.Add(node4); // Commands -> Add
            // Fetch
            node4 = new NcXmlFilterNode ("Fetch", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node5 = new NcXmlFilterNode ("ServerId", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // Fetch -> ServerId
            node3.Add(node4); // Commands -> Fetch
            node2.Add(node3); // Collection -> Commands
            node1.Add(node2); // Collections -> Collection
            node0.Add(node1); // Sync -> Collections
            // Wait
            node1 = new NcXmlFilterNode ("Wait", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Sync -> Wait
            // HeartbeatInterval
            node1 = new NcXmlFilterNode ("HeartbeatInterval", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Sync -> HeartbeatInterval
            // WindowSize
            node1 = new NcXmlFilterNode ("WindowSize", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Sync -> WindowSize
            // Partial
            node1 = new NcXmlFilterNode ("Partial", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Sync -> Partial
            
            Root = node0;
        }
    }
}
