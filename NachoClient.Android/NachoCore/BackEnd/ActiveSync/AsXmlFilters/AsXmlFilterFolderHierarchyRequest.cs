using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterFolderHierarchyRequest : NcXmlFilter
    {
        public AsXmlFilterFolderHierarchyRequest () : base ("FolderHierarchy")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;

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
            // SyncKey
            node2 = new NcXmlFilterNode ("SyncKey", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderCreate -> SyncKey
            // ParentId
            node2 = new NcXmlFilterNode ("ParentId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node1.Add(node2); // FolderCreate -> ParentId
            // DisplayName
            node2 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // FolderCreate -> DisplayName
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderCreate -> Type
            node0.Add(node1); // xml -> FolderCreate
            // FolderUpdate
            node1 = new NcXmlFilterNode ("FolderUpdate", RedactionType.NONE, RedactionType.NONE);
            // SyncKey
            node2 = new NcXmlFilterNode ("SyncKey", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderUpdate -> SyncKey
            // ServerId
            node2 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderUpdate -> ServerId
            // ParentId
            node2 = new NcXmlFilterNode ("ParentId", RedactionType.SHORT_HASH, RedactionType.NONE);
            node1.Add(node2); // FolderUpdate -> ParentId
            // DisplayName
            node2 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // FolderUpdate -> DisplayName
            node0.Add(node1); // xml -> FolderUpdate
            // FolderDelete
            node1 = new NcXmlFilterNode ("FolderDelete", RedactionType.NONE, RedactionType.NONE);
            // SyncKey
            node2 = new NcXmlFilterNode ("SyncKey", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderDelete -> SyncKey
            // ServerId
            node2 = new NcXmlFilterNode ("ServerId", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderDelete -> ServerId
            node0.Add(node1); // xml -> FolderDelete
            // FolderSync
            node1 = new NcXmlFilterNode ("FolderSync", RedactionType.NONE, RedactionType.NONE);
            // SyncKey
            node2 = new NcXmlFilterNode ("SyncKey", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // FolderSync -> SyncKey
            node0.Add(node1); // xml -> FolderSync
            
            Root = node0;
        }
    }
}
