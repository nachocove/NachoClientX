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

            // RightsManagementSupport
            node0 = new NcXmlFilterNode ("RightsManagementSupport", RedactionType.FULL, RedactionType.FULL);
            // TemplateID
            node0 = new NcXmlFilterNode ("TemplateID", RedactionType.FULL, RedactionType.FULL);
            // RemoveRightsManagementProtection
            node0 = new NcXmlFilterNode ("RemoveRightsManagementProtection", RedactionType.FULL, RedactionType.FULL);
            // RightsManagementTemplates
            node0 = new NcXmlFilterNode ("RightsManagementTemplates", RedactionType.NONE, RedactionType.NONE);
            // RightsManagementTemplate
            node1 = new NcXmlFilterNode ("RightsManagementTemplate", RedactionType.NONE, RedactionType.NONE);
            // TemplateID
            node2 = new NcXmlFilterNode ("TemplateID", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementTemplate -> TemplateID
            // TemplateName
            node2 = new NcXmlFilterNode ("TemplateName", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementTemplate -> TemplateName
            // TemplateDescription
            node2 = new NcXmlFilterNode ("TemplateDescription", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // RightsManagementTemplate -> TemplateDescription
            node0.Add(node1); // RightsManagementTemplates -> RightsManagementTemplate
            // RightsManagementLicense
            node0 = new NcXmlFilterNode ("RightsManagementLicense", RedactionType.NONE, RedactionType.NONE);
            // Owner
            node1 = new NcXmlFilterNode ("Owner", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> Owner
            // ContentOwner
            node1 = new NcXmlFilterNode ("ContentOwner", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> ContentOwner
            // ReplyAllAllowed
            node1 = new NcXmlFilterNode ("ReplyAllAllowed", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> ReplyAllAllowed
            // EditAllowed
            node1 = new NcXmlFilterNode ("EditAllowed", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> EditAllowed
            // ReplyAllowed
            node1 = new NcXmlFilterNode ("ReplyAllowed", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> ReplyAllowed
            // ForwardAllowed
            node1 = new NcXmlFilterNode ("ForwardAllowed", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> ForwardAllowed
            // ExportAllowed
            node1 = new NcXmlFilterNode ("ExportAllowed", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> ExportAllowed
            // ModifyRecipientsAllowed
            node1 = new NcXmlFilterNode ("ModifyRecipientsAllowed", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> ModifyRecipientsAllowed
            // TemplateID
            node1 = new NcXmlFilterNode ("TemplateID", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> TemplateID
            // ExtractAllowed
            node1 = new NcXmlFilterNode ("ExtractAllowed", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> ExtractAllowed
            // TemplateDescription
            node1 = new NcXmlFilterNode ("TemplateDescription", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> TemplateDescription
            // ContentExpiryDate
            node1 = new NcXmlFilterNode ("ContentExpiryDate", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> ContentExpiryDate
            // TemplateName
            node1 = new NcXmlFilterNode ("TemplateName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> TemplateName
            // PrintAllowed
            node1 = new NcXmlFilterNode ("PrintAllowed", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> PrintAllowed
            // ProgrammaticAccessAllowed
            node1 = new NcXmlFilterNode ("ProgrammaticAccessAllowed", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // RightsManagementLicense -> ProgrammaticAccessAllowed
            
            Root = node0;
        }
    }
}
