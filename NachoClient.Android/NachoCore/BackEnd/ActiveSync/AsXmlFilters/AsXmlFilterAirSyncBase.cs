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

            // FileReference
            node0 = new NcXmlFilterNode ("FileReference", RedactionType.FULL, RedactionType.FULL);
            // BodyPreference
            node0 = new NcXmlFilterNode ("BodyPreference", RedactionType.NONE, RedactionType.NONE);
            // Type
            node1 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPreference -> Type
            // TruncationSize
            node1 = new NcXmlFilterNode ("TruncationSize", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPreference -> TruncationSize
            // AllOrNone
            node1 = new NcXmlFilterNode ("AllOrNone", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPreference -> AllOrNone
            // Preview
            node1 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPreference -> Preview
            // BodyPartPreference
            node0 = new NcXmlFilterNode ("BodyPartPreference", RedactionType.NONE, RedactionType.NONE);
            // Type
            node1 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPartPreference -> Type
            // TruncationSize
            node1 = new NcXmlFilterNode ("TruncationSize", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPartPreference -> TruncationSize
            // AllOrNone
            node1 = new NcXmlFilterNode ("AllOrNone", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPartPreference -> AllOrNone
            // Preview
            node1 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPartPreference -> Preview
            // Body
            node0 = new NcXmlFilterNode ("Body", RedactionType.NONE, RedactionType.NONE);
            // Type
            node1 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Body -> Type
            // EstimatedDataSize
            node1 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Body -> EstimatedDataSize
            // Truncated
            node1 = new NcXmlFilterNode ("Truncated", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Body -> Truncated
            // Data
            node1 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Body -> Data
            // Preview
            node1 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Body -> Preview
            // BodyPart
            node0 = new NcXmlFilterNode ("BodyPart", RedactionType.NONE, RedactionType.NONE);
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> Status
            // Type
            node1 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> Type
            // EstimatedDataSize
            node1 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> EstimatedDataSize
            // Truncated
            node1 = new NcXmlFilterNode ("Truncated", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> Truncated
            // Data
            node1 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> Data
            // Preview
            node1 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> Preview
            // Attachments
            node0 = new NcXmlFilterNode ("Attachments", RedactionType.NONE, RedactionType.NONE);
            // Attachment
            node1 = new NcXmlFilterNode ("Attachment", RedactionType.NONE, RedactionType.NONE);
            // DisplayName
            node2 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> DisplayName
            // FileReference
            node2 = new NcXmlFilterNode ("FileReference", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> FileReference
            // Method
            node2 = new NcXmlFilterNode ("Method", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> Method
            // EstimatedDataSize
            node2 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> EstimatedDataSize
            // ContentId
            node2 = new NcXmlFilterNode ("ContentId", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> ContentId
            // ContentLocation
            node2 = new NcXmlFilterNode ("ContentLocation", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> ContentLocation
            // IsInline
            node2 = new NcXmlFilterNode ("IsInline", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> IsInline
            node0.Add(node1); // Attachments -> Attachment
            // NativeBodyType
            node0 = new NcXmlFilterNode ("NativeBodyType", RedactionType.FULL, RedactionType.FULL);
            // Body
            node0 = new NcXmlFilterNode ("Body", RedactionType.NONE, RedactionType.NONE);
            // Type
            node1 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Body -> Type
            // EstimatedDataSize
            node1 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Body -> EstimatedDataSize
            // Truncated
            node1 = new NcXmlFilterNode ("Truncated", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Body -> Truncated
            // Data
            node1 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Body -> Data
            // Preview
            node1 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // Body -> Preview
            // BodyPart
            node0 = new NcXmlFilterNode ("BodyPart", RedactionType.NONE, RedactionType.NONE);
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> Status
            // Type
            node1 = new NcXmlFilterNode ("Type", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> Type
            // EstimatedDataSize
            node1 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> EstimatedDataSize
            // Truncated
            node1 = new NcXmlFilterNode ("Truncated", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> Truncated
            // Data
            node1 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> Data
            // Preview
            node1 = new NcXmlFilterNode ("Preview", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // BodyPart -> Preview
            // Attachments
            node0 = new NcXmlFilterNode ("Attachments", RedactionType.NONE, RedactionType.NONE);
            // Attachment
            node1 = new NcXmlFilterNode ("Attachment", RedactionType.NONE, RedactionType.NONE);
            // DisplayName
            node2 = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> DisplayName
            // FileReference
            node2 = new NcXmlFilterNode ("FileReference", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> FileReference
            // Method
            node2 = new NcXmlFilterNode ("Method", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> Method
            // EstimatedDataSize
            node2 = new NcXmlFilterNode ("EstimatedDataSize", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> EstimatedDataSize
            // ContentId
            node2 = new NcXmlFilterNode ("ContentId", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> ContentId
            // ContentLocation
            node2 = new NcXmlFilterNode ("ContentLocation", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> ContentLocation
            // IsInline
            node2 = new NcXmlFilterNode ("IsInline", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Attachment -> IsInline
            node0.Add(node1); // Attachments -> Attachment
            // NativeBodyType
            node0 = new NcXmlFilterNode ("NativeBodyType", RedactionType.FULL, RedactionType.FULL);
            // Body
            node0 = new NcXmlFilterNode ("Body", RedactionType.FULL, RedactionType.FULL);
            // BodyPart
            node0 = new NcXmlFilterNode ("BodyPart", RedactionType.FULL, RedactionType.FULL);
            // Attachments
            node0 = new NcXmlFilterNode ("Attachments", RedactionType.FULL, RedactionType.FULL);
            
            Root = node0;
        }
    }
}
