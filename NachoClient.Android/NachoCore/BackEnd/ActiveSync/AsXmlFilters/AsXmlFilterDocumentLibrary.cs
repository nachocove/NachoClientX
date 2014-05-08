using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterDocumentLibrary : NcXmlFilter
    {
        public AsXmlFilterDocumentLibrary () : base ("DocumentLibrary")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // LinkId
            node1 = new NcXmlFilterNode ("LinkId", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> LinkId
            // DisplayName
            node1 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> DisplayName
            // IsFolder
            node1 = new NcXmlFilterNode ("IsFolder", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> IsFolder
            // CreationDate
            node1 = new NcXmlFilterNode ("CreationDate", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> CreationDate
            // LastModifiedDate
            node1 = new NcXmlFilterNode ("LastModifiedDate", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> LastModifiedDate
            // IsHidden
            node1 = new NcXmlFilterNode ("IsHidden", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> IsHidden
            // ContentLength
            node1 = new NcXmlFilterNode ("ContentLength", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ContentLength
            // ContentType
            node1 = new NcXmlFilterNode ("ContentType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ContentType
            
            Root = node0;
        }
    }
}
