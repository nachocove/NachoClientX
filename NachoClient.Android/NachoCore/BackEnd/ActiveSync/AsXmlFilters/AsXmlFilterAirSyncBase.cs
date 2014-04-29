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

            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // FileReference
            node1 = new NcXmlFilterNode ("FileReference", RedactionType.FULL, RedactionType.FULL);
            // BodyPreference
            node1 = new NcXmlFilterNode ("BodyPreference", RedactionType.NONE, RedactionType.NONE);
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPreference -> Type
            // TruncationSize
            node2 = new NcXmlFilterNode ("TruncationSize", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPreference -> TruncationSize
            // AllOrNone
            node2 = new NcXmlFilterNode ("AllOrNone", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPreference -> AllOrNone
            // Preview
            node2 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPreference -> Preview
            // BodyPartPreference
            node1 = new NcXmlFilterNode ("BodyPartPreference", RedactionType.NONE, RedactionType.NONE);
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPartPreference -> Type
            // TruncationSize
            node2 = new NcXmlFilterNode ("TruncationSize", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPartPreference -> TruncationSize
            // AllOrNone
            node2 = new NcXmlFilterNode ("AllOrNone", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPartPreference -> AllOrNone
            // Preview
            node2 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPartPreference -> Preview
            // Body
            node1 = new NcXmlFilterNode ("Body", RedactionType.NONE, RedactionType.NONE);
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> Type
            // EstimatedDataSize
            node2 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> EstimatedDataSize
            // Truncated
            node2 = new NcXmlFilterNode ("Truncated", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> Truncated
            // Data
            node2 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> Data
            // Preview
            node2 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> Preview
            // BodyPart
            node1 = new NcXmlFilterNode ("BodyPart", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Status
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Type
            // EstimatedDataSize
            node2 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> EstimatedDataSize
            // Truncated
            node2 = new NcXmlFilterNode ("Truncated", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Truncated
            // Data
            node2 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Data
            // Preview
            node2 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Preview
            // Attachments
            node1 = new NcXmlFilterNode ("Attachments", RedactionType.NONE, RedactionType.NONE);
            // Attachment
            node2 = new NcXmlFilterNode ("Attachment", RedactionType.NONE, RedactionType.NONE);
            // DisplayName
            node3 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> DisplayName
            // FileReference
            node3 = new NcXmlFilterNode ("FileReference", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> FileReference
            // Method
            node3 = new NcXmlFilterNode ("Method", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> Method
            // EstimatedDataSize
            node3 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> EstimatedDataSize
            // ContentId
            node3 = new NcXmlFilterNode ("ContentId", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> ContentId
            // ContentLocation
            node3 = new NcXmlFilterNode ("ContentLocation", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> ContentLocation
            // IsInline
            node3 = new NcXmlFilterNode ("IsInline", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> IsInline
            node1.Add(node2); // Attachments -> Attachment
            // NativeBodyType
            node1 = new NcXmlFilterNode ("NativeBodyType", RedactionType.FULL, RedactionType.FULL);
            // Body
            node1 = new NcXmlFilterNode ("Body", RedactionType.NONE, RedactionType.NONE);
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> Type
            // EstimatedDataSize
            node2 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> EstimatedDataSize
            // Truncated
            node2 = new NcXmlFilterNode ("Truncated", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> Truncated
            // Data
            node2 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> Data
            // Preview
            node2 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Body -> Preview
            // BodyPart
            node1 = new NcXmlFilterNode ("BodyPart", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Status
            // Type
            node2 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Type
            // EstimatedDataSize
            node2 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> EstimatedDataSize
            // Truncated
            node2 = new NcXmlFilterNode ("Truncated", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Truncated
            // Data
            node2 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Data
            // Preview
            node2 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // BodyPart -> Preview
            // Attachments
            node1 = new NcXmlFilterNode ("Attachments", RedactionType.NONE, RedactionType.NONE);
            // Attachment
            node2 = new NcXmlFilterNode ("Attachment", RedactionType.NONE, RedactionType.NONE);
            // DisplayName
            node3 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> DisplayName
            // FileReference
            node3 = new NcXmlFilterNode ("FileReference", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> FileReference
            // Method
            node3 = new NcXmlFilterNode ("Method", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> Method
            // EstimatedDataSize
            node3 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> EstimatedDataSize
            // ContentId
            node3 = new NcXmlFilterNode ("ContentId", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> ContentId
            // ContentLocation
            node3 = new NcXmlFilterNode ("ContentLocation", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> ContentLocation
            // IsInline
            node3 = new NcXmlFilterNode ("IsInline", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Attachment -> IsInline
            node1.Add(node2); // Attachments -> Attachment
            // NativeBodyType
            node1 = new NcXmlFilterNode ("NativeBodyType", RedactionType.FULL, RedactionType.FULL);
            // Body
            node1 = new NcXmlFilterNode ("Body", RedactionType.FULL, RedactionType.FULL);
            // BodyPart
            node1 = new NcXmlFilterNode ("BodyPart", RedactionType.FULL, RedactionType.FULL);
            // Attachments
            node1 = new NcXmlFilterNode ("Attachments", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1);
            
            Root = node0;
        }
    }
}
