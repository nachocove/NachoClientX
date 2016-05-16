using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterFolderHierarchyResponse : NcXmlFilter
    {
        public AsXmlFilterFolderHierarchyResponse () : base ("FolderHierarchy")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node1 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> ServerId
            // ParentId
            node1 = new NcXmlFilterNode ("ParentId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node0.Add(node1); // xml -> ParentId
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> Status
            // SyncKey
            node1 = new NcXmlFilterNode ("SyncKey", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> SyncKey
            // FolderCreate
            node1 = new NcXmlFilterNode ("FolderCreate", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderCreate -> Status
            // SyncKey
            node2 = new NcXmlFilterNode ("SyncKey", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderCreate -> SyncKey
            // ServerId
            node2 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderCreate -> ServerId
            node0.Add(node1); // xml -> FolderCreate
            // FolderUpdate
            node1 = new NcXmlFilterNode ("FolderUpdate", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderUpdate -> Status
            // SyncKey
            node2 = new NcXmlFilterNode ("SyncKey", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderUpdate -> SyncKey
            node0.Add(node1); // xml -> FolderUpdate
            // FolderDelete
            node1 = new NcXmlFilterNode ("FolderDelete", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderDelete -> Status
            // SyncKey
            node2 = new NcXmlFilterNode ("SyncKey", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderDelete -> SyncKey
            node0.Add(node1); // xml -> FolderDelete
            // FolderSync
            node1 = new NcXmlFilterNode ("FolderSync", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderSync -> Status
            // SyncKey
            node2 = new NcXmlFilterNode ("SyncKey", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderSync -> SyncKey
            // Changes
            node2 = new NcXmlFilterNode ("Changes", RedactionType.NONE, RedactionType.NONE);
            // Count
            node3 = new NcXmlFilterNode ("Count", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Changes -> Count
            // Update
            node3 = new NcXmlFilterNode ("Update", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node4 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Update -> ServerId
            // ParentId
            node4 = new NcXmlFilterNode ("ParentId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node3.Add(node4); // Update -> ParentId
            // DisplayName
            node4 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Update -> DisplayName
            // Type
            node4 = new NcXmlFilterNode ("Type", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Update -> Type
            node2.Add(node3); // Changes -> Update
            // Delete
            node3 = new NcXmlFilterNode ("Delete", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node4 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Delete -> ServerId
            node2.Add(node3); // Changes -> Delete
            // Add
            node3 = new NcXmlFilterNode ("Add", RedactionType.NONE, RedactionType.NONE);
            // ServerId
            node4 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Add -> ServerId
            // ParentId
            node4 = new NcXmlFilterNode ("ParentId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node3.Add(node4); // Add -> ParentId
            // DisplayName
            node4 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Add -> DisplayName
            // Type
            node4 = new NcXmlFilterNode ("Type", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Add -> Type
            node2.Add(node3); // Changes -> Add
            node1.Add(node2); // FolderSync -> Changes
            node0.Add(node1); // xml -> FolderSync
            
            Root = node0;
        }
    }
}
