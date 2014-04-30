using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterDocumentLibrary : NcXmlFilter
    {
        public AsXmlFilterDocumentLibrary () : base ("DocumentLibrary")
        {
            NcXmlFilterNode node0 = null;

            // LinkId
            node0 = new NcXmlFilterNode ("LinkId", RedactionType.FULL, RedactionType.FULL);
            // DisplayName
            node0 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            // IsFolder
            node0 = new NcXmlFilterNode ("IsFolder", RedactionType.FULL, RedactionType.FULL);
            // CreationDate
            node0 = new NcXmlFilterNode ("CreationDate", RedactionType.FULL, RedactionType.FULL);
            // LastModifiedDate
            node0 = new NcXmlFilterNode ("LastModifiedDate", RedactionType.FULL, RedactionType.FULL);
            // IsHidden
            node0 = new NcXmlFilterNode ("IsHidden", RedactionType.FULL, RedactionType.FULL);
            // ContentLength
            node0 = new NcXmlFilterNode ("ContentLength", RedactionType.FULL, RedactionType.FULL);
            // ContentType
            node0 = new NcXmlFilterNode ("ContentType", RedactionType.FULL, RedactionType.FULL);
            // LinkId
            node0 = new NcXmlFilterNode ("LinkId", RedactionType.FULL, RedactionType.FULL);
            // DisplayName
            node0 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            // IsFolder
            node0 = new NcXmlFilterNode ("IsFolder", RedactionType.FULL, RedactionType.FULL);
            // CreationDate
            node0 = new NcXmlFilterNode ("CreationDate", RedactionType.FULL, RedactionType.FULL);
            // LastModifiedDate
            node0 = new NcXmlFilterNode ("LastModifiedDate", RedactionType.FULL, RedactionType.FULL);
            // IsHidden
            node0 = new NcXmlFilterNode ("IsHidden", RedactionType.FULL, RedactionType.FULL);
            // ContentLength
            node0 = new NcXmlFilterNode ("ContentLength", RedactionType.FULL, RedactionType.FULL);
            // ContentType
            node0 = new NcXmlFilterNode ("ContentType", RedactionType.FULL, RedactionType.FULL);
            
            Root = node0;
        }
    }
}
