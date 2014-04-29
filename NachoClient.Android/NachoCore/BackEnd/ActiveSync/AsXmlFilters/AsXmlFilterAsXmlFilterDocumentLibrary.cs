using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterAsXmlFilterDocumentLibrary : NcXmlFilter
    {
        public AsXmlFilterAsXmlFilterDocumentLibrary () : base ("DocumentLibrary")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;

            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // LinkId
            node1 = new NcXmlFilterNode ("LinkId", RedactionType.FULL, RedactionType.FULL);
            // DisplayName
            node1 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            // IsFolder
            node1 = new NcXmlFilterNode ("IsFolder", RedactionType.FULL, RedactionType.FULL);
            // CreationDate
            node1 = new NcXmlFilterNode ("CreationDate", RedactionType.FULL, RedactionType.FULL);
            // LastModifiedDate
            node1 = new NcXmlFilterNode ("LastModifiedDate", RedactionType.FULL, RedactionType.FULL);
            // IsHidden
            node1 = new NcXmlFilterNode ("IsHidden", RedactionType.FULL, RedactionType.FULL);
            // ContentLength
            node1 = new NcXmlFilterNode ("ContentLength", RedactionType.FULL, RedactionType.FULL);
            // ContentType
            node1 = new NcXmlFilterNode ("ContentType", RedactionType.FULL, RedactionType.FULL);
            // LinkId
            node1 = new NcXmlFilterNode ("LinkId", RedactionType.FULL, RedactionType.FULL);
            // DisplayName
            node1 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            // IsFolder
            node1 = new NcXmlFilterNode ("IsFolder", RedactionType.FULL, RedactionType.FULL);
            // CreationDate
            node1 = new NcXmlFilterNode ("CreationDate", RedactionType.FULL, RedactionType.FULL);
            // LastModifiedDate
            node1 = new NcXmlFilterNode ("LastModifiedDate", RedactionType.FULL, RedactionType.FULL);
            // IsHidden
            node1 = new NcXmlFilterNode ("IsHidden", RedactionType.FULL, RedactionType.FULL);
            // ContentLength
            node1 = new NcXmlFilterNode ("ContentLength", RedactionType.FULL, RedactionType.FULL);
            // ContentType
            node1 = new NcXmlFilterNode ("ContentType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1);
            
            Root = node0;
        }
    }
}
