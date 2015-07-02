using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterAirSyncBase : NcXmlFilter
    {
        public AsXmlFilterAirSyncBase () : base ("AirSyncBase")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // FileReference
            node1 = new NcXmlFilterNode ("FileReference", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> FileReference
            // BodyPreference
            node1 = new NcXmlFilterNode ("BodyPreference", RedactionType.NONE, RedactionType.NONE);
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // BodyPreference -> Type
            // TruncationSize
            node2 = new NcXmlFilterNode ("TruncationSize", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // BodyPreference -> TruncationSize
            // AllOrNone
            node2 = new NcXmlFilterNode ("AllOrNone", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // BodyPreference -> AllOrNone
            // Preview
            node2 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPreference -> Preview
            node0.Add(node1); // xml -> BodyPreference
            // BodyPartPreference
            node1 = new NcXmlFilterNode ("BodyPartPreference", RedactionType.NONE, RedactionType.NONE);
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // BodyPartPreference -> Type
            // TruncationSize
            node2 = new NcXmlFilterNode ("TruncationSize", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // BodyPartPreference -> TruncationSize
            // AllOrNone
            node2 = new NcXmlFilterNode ("AllOrNone", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // BodyPartPreference -> AllOrNone
            // Preview
            node2 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPartPreference -> Preview
            node0.Add(node1); // xml -> BodyPartPreference
            // Body
            node1 = new NcXmlFilterNode ("Body", RedactionType.NONE, RedactionType.NONE);
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Body -> Type
            // EstimatedDataSize
            node2 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Body -> EstimatedDataSize
            // Truncated
            node2 = new NcXmlFilterNode ("Truncated", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Body -> Truncated
            // Data
            node2 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> Data
            // Preview
            node2 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> Preview
            node0.Add(node1); // xml -> Body
            // BodyPart
            node1 = new NcXmlFilterNode ("BodyPart", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // BodyPart -> Status
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // BodyPart -> Type
            // EstimatedDataSize
            node2 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // BodyPart -> EstimatedDataSize
            // Truncated
            node2 = new NcXmlFilterNode ("Truncated", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // BodyPart -> Truncated
            // Data
            node2 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Data
            // Preview
            node2 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Preview
            node0.Add(node1); // xml -> BodyPart
            // Attachments
            node1 = new NcXmlFilterNode ("Attachments", RedactionType.NONE, RedactionType.NONE);
            // Attachment
            node2 = new NcXmlFilterNode ("Attachment", RedactionType.NONE, RedactionType.NONE);
            // DisplayName
            node3 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> DisplayName
            // FileReference
            node3 = new NcXmlFilterNode ("FileReference", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Attachment -> FileReference
            // Method
            node3 = new NcXmlFilterNode ("Method", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> Method
            // EstimatedDataSize
            node3 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Attachment -> EstimatedDataSize
            // ContentId
            node3 = new NcXmlFilterNode ("ContentId", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> ContentId
            // ContentLocation
            node3 = new NcXmlFilterNode ("ContentLocation", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> ContentLocation
            // IsInline
            node3 = new NcXmlFilterNode ("IsInline", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // Attachment -> IsInline
            node1.Add(node2); // Attachments -> Attachment
            node0.Add(node1); // xml -> Attachments
            // NativeBodyType
            node1 = new NcXmlFilterNode ("NativeBodyType", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> NativeBodyType
            // ContentType
            node1 = new NcXmlFilterNode ("ContentType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> ContentType
            
            Root = node0;
        }
    }
}
