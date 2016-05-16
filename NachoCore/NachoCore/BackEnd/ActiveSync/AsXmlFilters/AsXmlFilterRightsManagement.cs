using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterRightsManagement : NcXmlFilter
    {
        public AsXmlFilterRightsManagement () : base ("RightsManagement")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // RightsManagementSupport
            node1 = new NcXmlFilterNode ("RightsManagementSupport", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> RightsManagementSupport
            // TemplateID
            node1 = new NcXmlFilterNode ("TemplateID", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> TemplateID
            // RemoveRightsManagementProtection
            node1 = new NcXmlFilterNode ("RemoveRightsManagementProtection", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> RemoveRightsManagementProtection
            // RightsManagementTemplates
            node1 = new NcXmlFilterNode ("RightsManagementTemplates", RedactionType.NONE, RedactionType.NONE);
            // RightsManagementTemplate
            node2 = new NcXmlFilterNode ("RightsManagementTemplate", RedactionType.NONE, RedactionType.NONE);
            // TemplateID
            node3 = new NcXmlFilterNode ("TemplateID", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // RightsManagementTemplate -> TemplateID
            // TemplateName
            node3 = new NcXmlFilterNode ("TemplateName", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // RightsManagementTemplate -> TemplateName
            // TemplateDescription
            node3 = new NcXmlFilterNode ("TemplateDescription", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // RightsManagementTemplate -> TemplateDescription
            node1.Add(node2); // RightsManagementTemplates -> RightsManagementTemplate
            node0.Add(node1); // xml -> RightsManagementTemplates
            // RightsManagementLicense
            node1 = new NcXmlFilterNode ("RightsManagementLicense", RedactionType.NONE, RedactionType.NONE);
            // Owner
            node2 = new NcXmlFilterNode ("Owner", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> Owner
            // ContentOwner
            node2 = new NcXmlFilterNode ("ContentOwner", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> ContentOwner
            // ReplyAllAllowed
            node2 = new NcXmlFilterNode ("ReplyAllAllowed", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> ReplyAllAllowed
            // EditAllowed
            node2 = new NcXmlFilterNode ("EditAllowed", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> EditAllowed
            // ReplyAllowed
            node2 = new NcXmlFilterNode ("ReplyAllowed", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> ReplyAllowed
            // ForwardAllowed
            node2 = new NcXmlFilterNode ("ForwardAllowed", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> ForwardAllowed
            // ExportAllowed
            node2 = new NcXmlFilterNode ("ExportAllowed", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> ExportAllowed
            // ModifyRecipientsAllowed
            node2 = new NcXmlFilterNode ("ModifyRecipientsAllowed", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> ModifyRecipientsAllowed
            // TemplateID
            node2 = new NcXmlFilterNode ("TemplateID", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> TemplateID
            // ExtractAllowed
            node2 = new NcXmlFilterNode ("ExtractAllowed", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> ExtractAllowed
            // TemplateDescription
            node2 = new NcXmlFilterNode ("TemplateDescription", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> TemplateDescription
            // ContentExpiryDate
            node2 = new NcXmlFilterNode ("ContentExpiryDate", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> ContentExpiryDate
            // TemplateName
            node2 = new NcXmlFilterNode ("TemplateName", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> TemplateName
            // PrintAllowed
            node2 = new NcXmlFilterNode ("PrintAllowed", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> PrintAllowed
            // ProgrammaticAccessAllowed
            node2 = new NcXmlFilterNode ("ProgrammaticAccessAllowed", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementLicense -> ProgrammaticAccessAllowed
            node0.Add(node1); // xml -> RightsManagementLicense
            
            Root = node0;
        }
    }
}
